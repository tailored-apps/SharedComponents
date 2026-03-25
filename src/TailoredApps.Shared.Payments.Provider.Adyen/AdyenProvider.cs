using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.Adyen;

// ─── Options ─────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja Adyen Checkout API. Sekcja: <c>Payments:Providers:Adyen</c>.</summary>
public class AdyenServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Adyen";

    /// <summary>Adyen API key (X-API-Key).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Identyfikator konta merchantskiego.</summary>
    public string MerchantAccount { get; set; } = string.Empty;

    /// <summary>Client key do Drop-in / Components (opcjonalnie).</summary>
    public string ClientKey { get; set; } = string.Empty;

    /// <summary>URL powrotu po płatności.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>HMAC klucz (hex) do weryfikacji powiadomień webhooka.</summary>
    public string NotificationHmacKey { get; set; } = string.Empty;

    /// <summary>Adyen environment name, e.g. "test" or "live". Used alongside <see cref="CheckoutUrl"/>.</summary>
    public string Environment { get; set; } = "test";

    /// <summary>
    /// Full base URL of the Adyen Checkout API (e.g. "https://checkout-test.adyen.com/v71").
    /// When set, takes precedence over the value derived from <see cref="IsTest"/>.
    /// </summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>
    /// True = test environment (checkout-test.adyen.com), False = production.
    /// Ignored when <see cref="CheckoutUrl"/> is explicitly set.
    /// </summary>
    public bool IsTest
    {
        get => Environment.Equals("test", StringComparison.OrdinalIgnoreCase);
        set => Environment = value ? "test" : "live";
    }
}

// ─── Internal models ─────────────────────────────────────────────────────────

file class AdyenAmount
{
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
}

file class AdyenSessionRequest
{
    [JsonPropertyName("merchantAccount")] public string MerchantAccount { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public AdyenAmount Amount { get; set; } = new();
    [JsonPropertyName("reference")] public string Reference { get; set; } = string.Empty;
    [JsonPropertyName("returnUrl")] public string ReturnUrl { get; set; } = string.Empty;
    [JsonPropertyName("shopperEmail")] public string? ShopperEmail { get; set; }
    [JsonPropertyName("countryCode")] public string? CountryCode { get; set; }
}

file class AdyenSessionResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("sessionData")] public string? SessionData { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

file class AdyenStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("resultCode")] public string? ResultCode { get; set; }
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad Adyen Checkout API v71 (Sessions).</summary>
public interface IAdyenServiceCaller
{
    /// <summary>Tworzy sesję płatności Adyen i zwraca id sesji oraz URL checkout.</summary>
    Task<(string? sessionId, string? checkoutUrl, string? error)> CreateSessionAsync(PaymentRequest request);

    /// <summary>Pobiera status płatności na podstawie pspReference lub sessionId.</summary>
    Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId);

    /// <summary>Weryfikuje HMAC podpis powiadomienia webhooka.</summary>
    bool VerifyNotificationHmac(string payload, string hmacSignature);
}

// ─── Caller ───────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IAdyenServiceCaller"/> oparta na Adyen Checkout REST API v71.</summary>
public class AdyenServiceCaller : IAdyenServiceCaller
{
    private readonly AdyenServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję <see cref="AdyenServiceCaller"/>.</summary>
    public AdyenServiceCaller(IOptions<AdyenServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    private string BaseUrl => !string.IsNullOrEmpty(options.CheckoutUrl)
        ? options.CheckoutUrl
        : options.IsTest
            ? "https://checkout-test.adyen.com/v71"
            : "https://checkout-live.adyen.com/v71";

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Adyen");
        client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <inheritdoc/>
    public async Task<(string? sessionId, string? checkoutUrl, string? error)> CreateSessionAsync(PaymentRequest request)
    {
        using var client = CreateClient();
        var body = new AdyenSessionRequest
        {
            MerchantAccount = options.MerchantAccount,
            Amount = new AdyenAmount { Value = (long)(request.Amount * 100), Currency = request.Currency.ToUpperInvariant() },
            Reference = request.AdditionalData ?? Guid.NewGuid().ToString("N"),
            ReturnUrl = options.ReturnUrl,
            ShopperEmail = request.Email,
            CountryCode = request.Country ?? "PL",
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/sessions", content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return (null, null, json);

        var result = JsonSerializer.Deserialize<AdyenSessionResponse>(json);
        return (result?.Id, result?.Url, null);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{BaseUrl}/payments/{paymentId}/details");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AdyenStatusResponse>(json);
        return result?.ResultCode switch
        {
            "Authorised" => PaymentStatusEnum.Finished,
            "Refused" => PaymentStatusEnum.Rejected,
            "Cancelled" => PaymentStatusEnum.Rejected,
            "Pending" => PaymentStatusEnum.Processing,
            "Received" => PaymentStatusEnum.Processing,
            _ => PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public bool VerifyNotificationHmac(string payload, string hmacSignature)
    {
        try
        {
            var keyBytes = Convert.FromHexString(options.NotificationHmacKey);
            var dataBytes = Encoding.UTF8.GetBytes(payload);
            var computed = Convert.ToBase64String(HMACSHA256.HashData(keyBytes, dataBytes));
            return string.Equals(computed, hmacSignature, StringComparison.Ordinal);
        }
        catch { return false; }
    }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Adyen Checkout.</summary>
public class AdyenProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IAdyenServiceCaller caller;

    /// <summary>Inicjalizuje instancję <see cref="AdyenProvider"/>.</summary>
    public AdyenProvider(IAdyenServiceCaller caller) => this.caller = caller;

    /// <inheritdoc/>
    public string Key => "Adyen";

    /// <inheritdoc/>
    public string Name => "Adyen";

    /// <inheritdoc/>
    public string Description => "Globalny operator płatności Adyen — karty, BLIK, iDEAL i inne.";

    /// <inheritdoc/>
    public string Url => "https://www.adyen.com";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels = currency.ToUpperInvariant() switch
        {
            "PLN" =>
            [
                new PaymentChannel { Id = "scheme",           Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "blik",             Name = "BLIK",            Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "onlineBanking_PL", Name = "Przelew online",  Description = "Polskie banki",    PaymentModel = PaymentModel.OneTime },
            ],
            "EUR" =>
            [
                new PaymentChannel { Id = "scheme",          Name = "Karta płatnicza",   Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "ideal",           Name = "iDEAL",             Description = "Przelew iDEAL",    PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "sepadirectdebit", Name = "SEPA Direct Debit", Description = "SEPA",             PaymentModel = PaymentModel.OneTime },
            ],
            _ =>
            [
                new PaymentChannel { Id = "scheme", Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            ],
        };
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var (sessionId, checkoutUrl, error) = await caller.CreateSessionAsync(request);

        if (sessionId is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = error };

        return new PaymentResponse
        {
            PaymentUniqueId = sessionId,
            RedirectUrl = checkoutUrl,
            PaymentStatus = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var status = await caller.GetPaymentStatusAsync(paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var body = request.Body ?? string.Empty;
        var hmac = request.Headers.TryGetValue("HmacSignature", out var h) ? h.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "HmacSignature", hmac },
            },
        };

        var response = await TransactionStatusChange(payload);

        if (response.PaymentStatus == PaymentStatusEnum.Rejected)
        {
            var msg = response.ResponseObject?.ToString() ?? string.Empty;
            if (msg.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("hash", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("sign", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("hmac", StringComparison.OrdinalIgnoreCase))
                return PaymentWebhookResult.Fail(msg);
        }

        if (response.PaymentStatus == PaymentStatusEnum.Processing && string.IsNullOrEmpty(response.PaymentUniqueId))
            return PaymentWebhookResult.Ignore("Non-actionable event");

        return PaymentWebhookResult.Ok(response);
    }

    /// <inheritdoc/>
    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        var hmac = payload.QueryParameters.TryGetValue("HmacSignature", out var h) ? h.ToString() : string.Empty;

        if (!caller.VerifyNotificationHmac(body, hmac))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid HMAC" });

        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Adyen sends notifications wrapped in notificationItems array
            JsonElement item = root;
            if (root.TryGetProperty("notificationItems", out var items) &&
                items.GetArrayLength() > 0 &&
                items[0].TryGetProperty("NotificationRequestItem", out var nri))
            {
                item = nri;
            }

            var eventCode = item.TryGetProperty("eventCode", out var ev) ? ev.GetString() : null;
            var success = item.TryGetProperty("success", out var s) ? s.GetString() : "true";
            var succeeded = !string.Equals(success, "false", StringComparison.OrdinalIgnoreCase);

            status = eventCode switch
            {
                "AUTHORISATION" => succeeded ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected,
                "CANCELLATION" => PaymentStatusEnum.Rejected,
                "REFUND" => succeeded ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected,
                "AUTHORISATION_FAILED" => PaymentStatusEnum.Rejected,
                _ => PaymentStatusEnum.Processing,
            };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla Adyen.</summary>
public static class AdyenProviderExtensions
{
    /// <summary>Rejestruje <see cref="AdyenProvider"/> i jego zależności w kontenerze DI.</summary>
    public static void RegisterAdyenProvider(this IServiceCollection services)
    {
        services.AddOptions<AdyenServiceOptions>();
        services.ConfigureOptions<AdyenConfigureOptions>();
        services.AddHttpClient("Adyen");
        services.AddTransient<IAdyenServiceCaller, AdyenServiceCaller>();
        services.AddTransient<AdyenProvider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<AdyenProvider>());
    }
}

/// <summary>Wczytuje opcje Adyen z konfiguracji aplikacji.</summary>
public class AdyenConfigureOptions : IConfigureOptions<AdyenServiceOptions>
{
    private readonly IConfiguration configuration;

    /// <summary>Inicjalizuje instancję <see cref="AdyenConfigureOptions"/>.</summary>
    public AdyenConfigureOptions(IConfiguration configuration) => this.configuration = configuration;

    /// <inheritdoc/>
    public void Configure(AdyenServiceOptions options)
    {
        var s = configuration.GetSection(AdyenServiceOptions.ConfigurationKey).Get<AdyenServiceOptions>();
        if (s is null) return;
        options.ApiKey = s.ApiKey;
        options.MerchantAccount = s.MerchantAccount;
        options.ClientKey = s.ClientKey;
        options.ReturnUrl = s.ReturnUrl;
        options.NotificationHmacKey = s.NotificationHmacKey;
        options.IsTest = s.IsTest;
    }
}
