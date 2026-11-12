using System;
using System.Collections.Generic;
using System.Threading;
using global::Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    /// <inheritdoc/>
    public string Key => StripeProviderKey;
    /// <inheritdoc/>
    public string Name => StripeProviderKey;
    /// <inheritdoc/>
    public string Description => "Globalny operator płatności kartą, BLIK i Przelewy24.";
    /// <inheritdoc/>
    public string Url => "https://stripe.com";

    /// <inheritdoc/>
    /// <remarks>
    /// Stripe nie udostępnia REST API do listy kanałów per waluta w modelu CashBill.
    /// Zwracamy statyczną listę bazując na walucie — identyczną z tą, którą StripeServiceCaller
    /// przekazuje do Checkout Session. Dzięki temu UI może wyświetlić dostępne opcje.
    /// </remarks>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency, CancellationToken cancellationToken = default)
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
    /// Stripe reports every error through <see cref="StripeException"/>; the provider sorts them the way
    /// the other providers do. When Stripe could not handle the call (5xx, 408, 429, or no answer at all)
    /// it throws <see cref="PaymentGatewayException"/> with the <see cref="StripeException"/> as its inner
    /// exception: the session may or may not exist. A 4xx answer is a refusal and returns
    /// <see cref="PaymentStatusEnum.Rejected"/> with Stripe's message in <see cref="PaymentResponse.ResponseObject"/>.
    /// </remarks>
    /// <exception cref="PaymentGatewayException">Stripe could not handle the call.</exception>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        global::Stripe.Checkout.Session session;
        try
        {
            session = await stripeCaller.CreateCheckoutSessionAsync(request, cancellationToken);
        }
        catch (StripeException exception) when (!IsRefusal(exception))
        {
            throw GatewayFailure(exception);
        }
        catch (StripeException exception)
        {
            // Invalid parameters, a payment method not enabled for the account, a wrong key, ...
            return new PaymentResponse
            {
                PaymentStatus = PaymentStatusEnum.Rejected,
                ResponseObject = exception.StripeError?.Message ?? exception.Message,
            };
        }

        return new PaymentResponse
        {
            PaymentUniqueId = session.Id,
            RedirectUrl = session.Url,
            PaymentStatus = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pobiera aktualny status Checkout Session. ID = Stripe Session ID (cs_...).
    /// Any <see cref="StripeException"/> becomes <see cref="PaymentGatewayException"/>: a failed status query
    /// says nothing about the payment - not even a 404 means it was rejected.
    /// </remarks>
    /// <exception cref="PaymentGatewayException">Stripe did not answer the query with a session.</exception>
    public async Task<PaymentResponse> GetStatus(string paymentId, CancellationToken cancellationToken = default)
    {
        global::Stripe.Checkout.Session session;
        try
        {
            session = await stripeCaller.GetCheckoutSessionAsync(paymentId, cancellationToken);
        }
        catch (StripeException exception)
        {
            throw GatewayFailure(exception);
        }

        return new PaymentResponse
        {
            PaymentUniqueId = session.Id,
            RedirectUrl = session.Url,
            PaymentStatus = MapSessionStatus(session),
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
    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload, CancellationToken cancellationToken = default)
        => Task.FromResult(ProcessEvent(payload).ToPaymentResponse());

    private PaymentWebhookResult ProcessEvent(TransactionStatusChangePayload payload)
    {
        var rawBody = payload.Payload?.ToString() ?? string.Empty;
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
            return PaymentWebhookResult.Reject(
                PaymentWebhookRejection.InvalidSignature,
                $"Webhook signature verification failed: {ex.Message}");
        }

        var response = stripeEvent.Type switch
        {
            "checkout.session.completed" =>
                HandleSessionCompleted(stripeEvent),
            "checkout.session.async_payment_succeeded" =>
                SessionResponse(stripeEvent, PaymentStatusEnum.Finished),
            "checkout.session.async_payment_failed" =>
                SessionResponse(stripeEvent, PaymentStatusEnum.Rejected),
            "checkout.session.expired" =>
                SessionResponse(stripeEvent, PaymentStatusEnum.Rejected),
            "payment_intent.succeeded" =>
                PaymentIntentResponse(stripeEvent, PaymentStatusEnum.Finished),
            "payment_intent.payment_failed" =>
                PaymentIntentResponse(stripeEvent, PaymentStatusEnum.Rejected),
            _ => new PaymentResponse
            {
                PaymentStatus = PaymentStatusEnum.Processing,
                ResponseObject = "OK",
            },
        };

        return PaymentWebhookResult.Ok(response);
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <summary>
    /// Handles an incoming Stripe webhook HTTP request.
    /// Extracts the <c>Stripe-Signature</c> header, verifies the HMAC-SHA256 signature,
    /// parses the event type and returns a normalised <see cref="PaymentWebhookResult"/>.
    /// </summary>
    /// <remarks>
    /// Events that do not represent a terminal payment state
    /// (e.g. <c>payment_method.attached</c>) result in <see cref="PaymentWebhookIgnored"/>.
    /// </remarks>
    /// <param name="request">Unified HTTP webhook request.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var rawBody = request.Body ?? string.Empty;
        var signature = request.Headers.TryGetValue("Stripe-Signature", out var sig)
            ? sig.ToString()
            : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload = rawBody,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "Stripe-Signature", signature },
            },
        };

        var result = ProcessEvent(payload);

        // Non-actionable event (payment_method.attached, customer.created, ...) or one without a payment id
        if (result is PaymentWebhookProcessed { Payment: var payment }
            && (payment.PaymentStatus == PaymentStatusEnum.Processing || string.IsNullOrEmpty(payment.PaymentUniqueId)))
        {
            return Task.FromResult(PaymentWebhookResult.Ignore("Non-actionable Stripe event", payment));
        }

        return Task.FromResult(result);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// A 4xx answer from Stripe refuses the request. 5xx, 408 and 429 mean Stripe could not handle it, and
    /// an exception without an HTTP status did not come from an answer at all.
    /// </summary>
    private static bool IsRefusal(StripeException exception)
        => exception.HttpStatusCode != 0 && !PaymentGatewayException.IsUnavailable(exception.HttpStatusCode);

    private PaymentGatewayException GatewayFailure(StripeException exception)
        => new(Key, exception.HttpStatusCode, exception.StripeResponse?.Content, exception);

    private static PaymentResponse HandleSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        var status = session?.PaymentStatus == "paid"
            ? PaymentStatusEnum.Finished
            : PaymentStatusEnum.Processing;

        return new PaymentResponse
        {
            PaymentUniqueId = session?.Id,
            RedirectUrl = session?.Url,
            PaymentStatus = status,
            ResponseObject = "OK",
        };
    }

    private static PaymentResponse SessionResponse(Event stripeEvent, PaymentStatusEnum status)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        return new PaymentResponse
        {
            PaymentUniqueId = session?.Id,
            RedirectUrl = session?.Url,
            PaymentStatus = status,
            ResponseObject = "OK",
        };
    }

    private static PaymentResponse PaymentIntentResponse(Event stripeEvent, PaymentStatusEnum status)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        return new PaymentResponse
        {
            PaymentUniqueId = paymentIntent?.Id,
            PaymentStatus = status,
            ResponseObject = "OK",
        };
    }

    private static PaymentStatusEnum MapSessionStatus(global::Stripe.Checkout.Session session) =>
        session.Status switch
        {
            "complete" when session.PaymentStatus == "paid" => PaymentStatusEnum.Finished,
            "complete" => PaymentStatusEnum.Processing,
            "expired" => PaymentStatusEnum.Rejected,
            _ => PaymentStatusEnum.Created,   // "open"
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
        // Fail when the host starts, not on the first payment: these are the settings the provider cannot work without.
        services.AddOptions<StripeServiceOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), $"'{StripeServiceOptions.ConfigurationKey}:SecretKey' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.WebhookSecret), $"'{StripeServiceOptions.ConfigurationKey}:WebhookSecret' is not configured.")
            .ValidateOnStart();
        services.ConfigureOptions<StripeConfigureOptions>();
        services.AddTransient<global::Stripe.Checkout.SessionService>();
        services.AddTransient<IStripeServiceCaller, StripeServiceCaller>();

        // Register as both IPaymentProvider (for PaymentService aggregator)
        // and IWebhookPaymentProvider (for webhook dispatch).
        services.AddTransient<StripeProvider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<StripeProvider>());
    }

    /// <summary>
    /// Registers the Stripe provider with its dependencies and adds it to the payment service, so that
    /// <see cref="IPaymentService"/> can route to it. This is the one call a host needs for this provider:
    /// <c>builder.Services.AddPayments().RegisterStripeProvider();</c>
    /// </summary>
    /// <param name="builder">The builder returned by <c>AddPayments()</c>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IPaymentOptionsBuilder RegisterStripeProvider(this IPaymentOptionsBuilder builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RegisterStripeProvider();

        // A host that also calls RegisterPaymentProvider<StripeProvider>() must not end up with the provider twice.
        foreach (var registered in builder.Services)
        {
            if (registered.ServiceType == typeof(IPaymentProvider) && registered.ImplementationType == typeof(StripeProvider))
                return builder;
        }

        return builder.RegisterPaymentProvider<StripeProvider>();
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

        options.SecretKey = section.SecretKey;
        options.WebhookSecret = section.WebhookSecret;
        options.SuccessUrl = section.SuccessUrl;
        options.CancelUrl = section.CancelUrl;
    }
}
