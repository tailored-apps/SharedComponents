using System.Security.Cryptography;
using System.Text;
using Adyen;
using Adyen.Model.Checkout;
using Adyen.Service.Checkout;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Environment = Adyen.Model.Enum.Environment;

namespace TailoredApps.Shared.Payments.Provider.Adyen;

// ─── Options ─────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja Adyen. Sekcja: <c>Payments:Providers:Adyen</c>.</summary>
public class AdyenServiceOptions
{
    public static string ConfigurationKey => "Payments:Providers:Adyen";
    public string ApiKey              { get; set; } = string.Empty;
    public string MerchantAccount     { get; set; } = string.Empty;
    public string ClientKey           { get; set; } = string.Empty;
    public string ReturnUrl           { get; set; } = string.Empty;
    /// <summary>HMAC klucz do weryfikacji powiadomień webhooka.</summary>
    public string NotificationHmacKey { get; set; } = string.Empty;
    /// <summary>True = sandbox (test), False = live.</summary>
    public bool   IsTest              { get; set; } = true;
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad Adyen Checkout API.</summary>
public interface IAdyenServiceCaller
{
    Task<CreateCheckoutSessionResponse> CreateSessionAsync(PaymentRequest request);
    Task<PaymentDetailsResponse> GetPaymentStatusAsync(string pspReference);
    bool VerifyNotificationHmac(string payload, string hmacSignature);
}

// ─── Caller ───────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IAdyenServiceCaller"/> opakowująca oficjalny Adyen SDK.</summary>
public class AdyenServiceCaller : IAdyenServiceCaller
{
    private readonly AdyenServiceOptions options;

    public AdyenServiceCaller(IOptions<AdyenServiceOptions> options)
    {
        this.options = options.Value;
    }

    private Client CreateClient()
    {
        var config = new Config
        {
            XApiKey     = options.ApiKey,
            Environment = options.IsTest ? Environment.Test : Environment.Live,
        };
        return new Client(config);
    }

    public async Task<CreateCheckoutSessionResponse> CreateSessionAsync(PaymentRequest request)
    {
        var client   = CreateClient();
        var checkout = new PaymentsService(client);

        var amount = new Adyen.Model.Checkout.Amount
        {
            Value    = (long)(request.Amount * 100),
            Currency = request.Currency.ToUpperInvariant(),
        };

        var sessionRequest = new CreateCheckoutSessionRequest(
            merchantAccount: options.MerchantAccount,
            amount:          amount,
            returnUrl:       options.ReturnUrl,
            reference:       request.AdditionalData ?? Guid.NewGuid().ToString("N")
        )
        {
            ShopperEmail     = request.Email,
            ShopperReference = request.Email,
            CountryCode      = request.Country ?? "PL",
        };

        return await checkout.SessionsAsync(sessionRequest);
    }

    public async Task<PaymentDetailsResponse> GetPaymentStatusAsync(string pspReference)
    {
        var client   = CreateClient();
        var checkout = new PaymentsService(client);
        var details  = new PaymentDetailsRequest(details: new PaymentCompletionDetails(), paymentData: pspReference);
        return await checkout.PaymentsDetailsAsync(details);
    }

    public bool VerifyNotificationHmac(string payload, string hmacSignature)
    {
        try
        {
            var keyBytes  = Convert.FromHexString(options.NotificationHmacKey);
            var dataBytes = Encoding.UTF8.GetBytes(payload);
            var computed  = Convert.ToBase64String(HMACSHA256.HashData(keyBytes, dataBytes));
            return string.Equals(computed, hmacSignature, StringComparison.Ordinal);
        }
        catch { return false; }
    }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Adyen.</summary>
public class AdyenProvider : IPaymentProvider
{
    private readonly IAdyenServiceCaller caller;
    private readonly AdyenServiceOptions options;

    public AdyenProvider(IAdyenServiceCaller caller, IOptions<AdyenServiceOptions> options)
    {
        this.caller  = caller;
        this.options = options.Value;
    }

    public string Key         => "Adyen";
    public string Name        => "Adyen";
    public string Description => "Globalny operator płatności Adyen — karty, BLIK, iDEAL i inne.";
    public string Url         => "https://www.adyen.com";

    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels = currency.ToUpperInvariant() switch
        {
            "PLN" =>
            [
                new PaymentChannel { Id = "scheme",        Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "blik",          Name = "BLIK",            Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "onlineBanking_PL", Name = "Przelew online", Description = "Polskie banki",  PaymentModel = PaymentModel.OneTime },
            ],
            "EUR" =>
            [
                new PaymentChannel { Id = "scheme", Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "ideal",  Name = "iDEAL",           Description = "Przelew iDEAL",    PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "sepadirectdebit", Name = "SEPA Direct Debit", Description = "SEPA", PaymentModel = PaymentModel.OneTime },
            ],
            _ =>
            [
                new PaymentChannel { Id = "scheme", Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            ],
        };
        return Task.FromResult(channels);
    }

    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var session = await caller.CreateSessionAsync(request);
        return new PaymentResponse
        {
            PaymentUniqueId = session.Id,
            RedirectUrl     = session.Url,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var details = await caller.GetPaymentStatusAsync(paymentId);
        var status = details.ResultCode?.ToString() switch
        {
            "Authorised"           => PaymentStatusEnum.Finished,
            "Refused"              => PaymentStatusEnum.Rejected,
            "Cancelled"            => PaymentStatusEnum.Rejected,
            "Pending"              => PaymentStatusEnum.Processing,
            "Received"             => PaymentStatusEnum.Processing,
            _                      => PaymentStatusEnum.Created,
        };
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        var hmac = payload.QueryParameters.TryGetValue("HmacSignature", out var h) ? h.ToString() : string.Empty;

        if (!caller.VerifyNotificationHmac(body, hmac))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid HMAC" });

        // Parsuj eventCode z JSON
        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("eventCode", out var ev))
            {
                status = ev.GetString() switch
                {
                    "AUTHORISATION"         => PaymentStatusEnum.Finished,
                    "CANCELLATION"          => PaymentStatusEnum.Rejected,
                    "REFUND"                => PaymentStatusEnum.Rejected,
                    "AUTHORISATION_FAILED"  => PaymentStatusEnum.Rejected,
                    _                       => PaymentStatusEnum.Processing,
                };
            }
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla Adyen.</summary>
public static class AdyenProviderExtensions
{
    public static void RegisterAdyenProvider(this IServiceCollection services)
    {
        services.AddOptions<AdyenServiceOptions>();
        services.ConfigureOptions<AdyenConfigureOptions>();
        services.AddTransient<IAdyenServiceCaller, AdyenServiceCaller>();
    }
}

/// <summary>Wczytuje opcje Adyen z konfiguracji.</summary>
public class AdyenConfigureOptions : IConfigureOptions<AdyenServiceOptions>
{
    private readonly IConfiguration configuration;
    public AdyenConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    public void Configure(AdyenServiceOptions options)
    {
        var s = configuration.GetSection(AdyenServiceOptions.ConfigurationKey).Get<AdyenServiceOptions>();
        if (s is null) return;
        options.ApiKey              = s.ApiKey;
        options.MerchantAccount     = s.MerchantAccount;
        options.ClientKey           = s.ClientKey;
        options.ReturnUrl           = s.ReturnUrl;
        options.NotificationHmacKey = s.NotificationHmacKey;
        options.IsTest              = s.IsTest;
    }
}
