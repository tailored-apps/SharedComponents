using global::Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Shared.Payments.Provider.Stripe;

/// <summary>
/// Implementacja <see cref="IPaymentProvider"/> dla Stripe Checkout.
/// Przepływ: RequestPayment → Stripe Checkout Session (hosted page) → webhook StatusChange.
/// </summary>
public class StripeProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IStripeServiceCaller stripeCaller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public StripeProvider(IStripeServiceCaller stripeCaller)
    {
        this.stripeCaller = stripeCaller;
    }

    private static readonly string StripeProviderKey = "Stripe";

    public string Key         => StripeProviderKey;
    public string Name        => StripeProviderKey;
    /// <inheritdoc/>
    public string Description => "Globalny operator płatności kartą, BLIK i Przelewy24.";
    public string Url         => "https://stripe.com";

    /// <inheritdoc/>
    /// <remarks>
    /// Stripe nie udostępnia REST API do listy kanałów per waluta w modelu CashBill.
    /// Zwracamy statyczną listę bazując na walucie — identyczną z tą, którą StripeServiceCaller
    /// przekazuje do Checkout Session. Dzięki temu UI może wyświetlić dostępne opcje.
    /// </remarks>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels = currency.ToUpperInvariant() switch
        {
            "PLN" =>
            [
                new PaymentChannel { Id = "card", Name = "Karta płatnicza",  Description = "Visa, Mastercard, American Express", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "blik", Name = "BLIK",             Description = "Szybka płatność kodem BLIK",         PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "p24",  Name = "Przelewy24",       Description = "Szybki przelew bankowy",             PaymentModel = PaymentModel.OneTime },
            ],
            "EUR" =>
            [
                new PaymentChannel { Id = "card",       Name = "Karta płatnicza", Description = "Visa, Mastercard, American Express", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "sepa_debit", Name = "SEPA Direct Debit", Description = "Polecenie zapłaty SEPA",           PaymentModel = PaymentModel.OneTime },
            ],
            _ =>
            [
                new PaymentChannel { Id = "card", Name = "Karta płatnicza", Description = "Visa, Mastercard, American Express", PaymentModel = PaymentModel.OneTime },
            ],
        };

        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Tworzy Stripe Checkout Session. Zwraca URL do hosted page Stripe oraz ID sesji.
    /// </remarks>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var session = await stripeCaller.CreateCheckoutSessionAsync(request);

        return new PaymentResponse
        {
            PaymentUniqueId = session.Id,
            RedirectUrl     = session.Url,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pobiera aktualny status Checkout Session. ID = Stripe Session ID (cs_...).
    /// </remarks>
    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var session = await stripeCaller.GetCheckoutSessionAsync(paymentId);

        return new PaymentResponse
        {
            PaymentUniqueId = session.Id,
            RedirectUrl     = session.Url,
            PaymentStatus   = MapSessionStatus(session),
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Obsługuje webhook Stripe. Oczekuje:
    /// <list type="bullet">
    ///   <item><c>payload.Payload</c> — surowe body HTTP (string JSON).</item>
    ///   <item><c>payload.QueryParameters["Stripe-Signature"]</c> — wartość nagłówka Stripe-Signature.</item>
    /// </list>
    /// Weryfikuje podpis HMAC-SHA256 i przetwarza zdarzenia:
    /// checkout.session.completed, checkout.session.expired.
    /// </remarks>
    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var rawBody       = payload.Payload?.ToString() ?? string.Empty;
        var stripeSignature = payload.QueryParameters.TryGetValue("Stripe-Signature", out var sig)
            ? sig.ToString()
            : string.Empty;

        Event stripeEvent;
        try
        {
            stripeEvent = stripeCaller.ConstructWebhookEvent(rawBody, stripeSignature);
        }
        catch (StripeException ex)
        {
            // Nieprawidłowy podpis — zwracamy Rejected i nie przetwarzamy zdarzenia.
            return Task.FromResult(new PaymentResponse
            {
                PaymentStatus   = PaymentStatusEnum.Rejected,
                ResponseObject  = $"Webhook signature verification failed: {ex.Message}",
            });
        }

        var response = stripeEvent.Type switch
        {
            "checkout.session.completed" =>
                HandleSessionCompleted(stripeEvent),
            "checkout.session.expired" =>
                new PaymentResponse
                {
                    PaymentStatus  = PaymentStatusEnum.Rejected,
                    ResponseObject = "OK",
                },
            "payment_intent.succeeded" =>
                new PaymentResponse
                {
                    PaymentStatus  = PaymentStatusEnum.Finished,
                    ResponseObject = "OK",
                },
            "payment_intent.payment_failed" =>
                new PaymentResponse
                {
                    PaymentStatus  = PaymentStatusEnum.Rejected,
                    ResponseObject = "OK",
                },
            _ => new PaymentResponse
            {
                PaymentStatus  = PaymentStatusEnum.Processing,
                ResponseObject = "OK",
            },
        };

        return Task.FromResult(response);
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <summary>
    /// Handles an incoming Stripe webhook HTTP request.
    /// Extracts the <c>Stripe-Signature</c> header, verifies the HMAC-SHA256 signature,
    /// parses the event type and returns a normalised <see cref="PaymentWebhookResult"/>.
    /// </summary>
    /// <remarks>
    /// Events that do not represent a terminal payment state
    /// (e.g. <c>payment_method.attached</c>) result in <see cref="PaymentWebhookResult.Ignore"/>.
    /// </remarks>
    /// <param name="request">Unified HTTP webhook request.</param>
    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var rawBody   = request.Body ?? string.Empty;
        var signature = request.Headers.TryGetValue("Stripe-Signature", out var sig)
            ? sig.ToString()
            : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload          = rawBody,
            QueryParameters  = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "Stripe-Signature", signature },
            },
        };

        var response = ((IPaymentProvider)this).TransactionStatusChange(payload).GetAwaiter().GetResult();

        // Signature failure — TransactionStatusChange returns Rejected + message containing "signature"
        if (response.PaymentStatus == PaymentStatusEnum.Rejected
            && response.ResponseObject?.ToString()?.Contains("signature", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Task.FromResult(PaymentWebhookResult.Fail(response.ResponseObject?.ToString() ?? "Invalid signature"));
        }

        // Non-actionable event (payment_method.attached, customer.created, …)
        if (response.PaymentStatus == PaymentStatusEnum.Processing
            || string.IsNullOrEmpty(response.PaymentUniqueId))
        {
            return Task.FromResult(PaymentWebhookResult.Ignore("Non-actionable Stripe event"));
        }

        return Task.FromResult(PaymentWebhookResult.Ok(response));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static PaymentResponse HandleSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        var status  = session?.PaymentStatus == "paid"
            ? PaymentStatusEnum.Finished
            : PaymentStatusEnum.Processing;

        return new PaymentResponse
        {
            PaymentUniqueId = session?.Id,
            RedirectUrl     = session?.Url,
            PaymentStatus   = status,
            ResponseObject  = "OK",
        };
    }

    private static PaymentStatusEnum MapSessionStatus(global::Stripe.Checkout.Session session) =>
        session.Status switch
        {
            "complete" when session.PaymentStatus == "paid" => PaymentStatusEnum.Finished,
            "complete"  => PaymentStatusEnum.Processing,
            "expired"   => PaymentStatusEnum.Rejected,
            _           => PaymentStatusEnum.Created,   // "open"
        };
}

// ─── DI Extensions ──────────────────────────────────────────────────────────

/// <summary>
/// Rozszerzenia DI analogiczne do <c>CashBillProviderExtensions.RegisterCashbillProvider()</c>.
/// </summary>
public static class StripeProviderExtensions
{
    /// <summary>
    /// Rejestruje wszystkie usługi wymagane przez <see cref="StripeProvider"/>:
    /// <see cref="StripeServiceOptions"/> (konfiguracja), <see cref="IStripeServiceCaller"/>,
    /// i Stripe.net <c>SessionService</c>.
    /// </summary>
    public static void RegisterStripeProvider(this IServiceCollection services)
    {
        services.AddOptions<StripeServiceOptions>();
        services.ConfigureOptions<StripeConfigureOptions>();
        services.AddTransient<global::Stripe.Checkout.SessionService>();
        services.AddTransient<IStripeServiceCaller, StripeServiceCaller>();

        // Register as both IPaymentProvider (for PaymentService aggregator)
        // and IWebhookPaymentProvider (for webhook dispatch).
        services.AddTransient<StripeProvider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<StripeProvider>());
    }
}

/// <summary>
/// Wczytuje opcje Stripe z sekcji <see cref="StripeServiceOptions.ConfigurationKey"/>.
/// </summary>
public class StripeConfigureOptions : IConfigureOptions<StripeServiceOptions>
{
    private readonly IConfiguration configuration;

    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public StripeConfigureOptions(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <inheritdoc/>
    public void Configure(StripeServiceOptions options)
    {
        var section = configuration
            .GetSection(StripeServiceOptions.ConfigurationKey)
            .Get<StripeServiceOptions>();

        if (section is null) return;

        options.SecretKey     = section.SecretKey;
        options.WebhookSecret = section.WebhookSecret;
        options.SuccessUrl    = section.SuccessUrl;
        options.CancelUrl     = section.CancelUrl;
    }
}
