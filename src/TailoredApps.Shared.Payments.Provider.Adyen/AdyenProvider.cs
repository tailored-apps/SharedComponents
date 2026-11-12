using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Payments.Security;

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
    Task<(string? sessionId, string? checkoutUrl, string? error)> CreateSessionAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Pobiera status płatności na podstawie pspReference lub sessionId.</summary>
    Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default);

    /// <summary>Weryfikuje HMAC podpis powiadomienia webhooka.</summary>
    /// <param name="payload">
    /// The signed data of one notification item: <c>pspReference</c>, <c>originalReference</c>,
    /// <c>merchantAccountCode</c>, <c>merchantReference</c>, <c>amount.value</c>, <c>amount.currency</c>,
    /// <c>eventCode</c> and <c>success</c> joined with <c>:</c> (an absent field is an empty string).
    /// </param>
    /// <param name="hmacSignature">The item's <c>additionalData.hmacSignature</c> (Base64).</param>
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
    public async Task<(string? sessionId, string? checkoutUrl, string? error)> CreateSessionAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var body = new AdyenSessionRequest
        {
            MerchantAccount = options.MerchantAccount,
            Amount = new AdyenAmount { Value = (long)(request.Amount * 100), Currency = (request.Currency ?? throw new ArgumentException("Currency is required.", nameof(request))).ToUpperInvariant() },
            Reference = request.AdditionalData ?? Guid.NewGuid().ToString("N"),
            ReturnUrl = options.ReturnUrl,
            ShopperEmail = request.Email,
            CountryCode = request.Country ?? "PL",
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/sessions", content, cancellationToken);
        // 5xx, 408 and 429 mean the gateway did not handle the call; a 4xx answer is a refusal reported below.
        await PaymentGatewayException.ThrowIfUnavailableAsync("Adyen", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, null, json);

        var result = JsonSerializer.Deserialize<AdyenSessionResponse>(json);
        return (result?.Id, result?.Url, null);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var safePaymentId = PaymentIdentifier.EnsureSafe(paymentId, nameof(paymentId));
        var response = await client.GetAsync($"{BaseUrl}/payments/{safePaymentId}/details", cancellationToken);
        // A failed status query says nothing about the payment: never report it as rejected.
        await PaymentGatewayException.ThrowIfNotSuccessAsync("Adyen", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
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
    /// <remarks>
    /// HMAC-SHA256 over the UTF-8 bytes of <paramref name="payload"/>, keyed with the hex-decoded
    /// <see cref="AdyenServiceOptions.NotificationHmacKey"/>, Base64-encoded and compared in constant time.
    /// </remarks>
    public bool VerifyNotificationHmac(string payload, string hmacSignature)
    {
        // Fail closed: an empty HMAC key would let anyone compute a "valid" signature.
        if (!WebhookSignature.IsSecretConfigured(options.NotificationHmacKey) || string.IsNullOrEmpty(hmacSignature))
            return false;

        try
        {
            var keyBytes = Convert.FromHexString(options.NotificationHmacKey);
            if (keyBytes.Length == 0) return false;
            var dataBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            var computed = Convert.ToBase64String(HMACSHA256.HashData(keyBytes, dataBytes));
            return WebhookSignature.FixedTimeEquals(computed, hmacSignature);
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
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency, CancellationToken cancellationToken = default)
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
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var (sessionId, checkoutUrl, error) = await caller.CreateSessionAsync(request, cancellationToken);

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
    public async Task<PaymentResponse> GetStatus(string paymentId, CancellationToken cancellationToken = default)
    {
        var status = await caller.GetPaymentStatusAsync(paymentId, cancellationToken);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <summary>
    /// The body Adyen expects in the acknowledgement of a standard webhook. Returned in
    /// <see cref="PaymentResponse.ResponseObject"/>; answer with HTTP 200 and this text.
    /// </summary>
    public const string Acknowledgement = "[accepted]";

    /// <inheritdoc/>
    /// <remarks>
    /// A standard Adyen webhook carries no signature header: the signature of each item is in its
    /// <c>additionalData.hmacSignature</c> and covers eight of the item's fields, not the request body.
    /// Only the first item of <c>notificationItems</c> is handled (Adyen sends one item per request).
    /// </remarks>
    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ProcessNotification(request.Body));

    /// <inheritdoc/>
    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload, CancellationToken cancellationToken = default)
        => Task.FromResult(ProcessNotification(payload.Payload?.ToString()).ToPaymentResponse());

    private PaymentWebhookResult ProcessNotification(string? body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body ?? string.Empty);
        }
        catch (JsonException)
        {
            return PaymentWebhookResult.Reject(PaymentWebhookRejection.MalformedPayload, "The notification is not valid JSON.");
        }

        using (document)
        {
            var item = FindNotificationItem(document.RootElement);
            if (item.ValueKind != JsonValueKind.Object)
                return PaymentWebhookResult.Reject(PaymentWebhookRejection.MalformedPayload, "The notification has no NotificationRequestItem.");

            var additionalData = item.TryGetProperty("additionalData", out var data) ? data : default;
            if (!caller.VerifyNotificationHmac(BuildSignedData(item), Text(additionalData, "hmacSignature")))
                return PaymentWebhookResult.Reject(PaymentWebhookRejection.InvalidSignature, "Invalid HMAC");

            // A missing "success" flag must never be treated as success.
            var succeeded = string.Equals(Text(item, "success"), "true", StringComparison.OrdinalIgnoreCase);
            var status = Text(item, "eventCode") switch
            {
                "AUTHORISATION" => succeeded ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected,
                "CANCELLATION" => PaymentStatusEnum.Rejected,
                "REFUND" => succeeded ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected,
                "AUTHORISATION_FAILED" => PaymentStatusEnum.Rejected,
                _ => PaymentStatusEnum.Processing,
            };

            var merchantReference = Text(item, "merchantReference");
            var pspReference = Text(item, "pspReference");
            var paymentUniqueId = merchantReference.Length > 0 ? merchantReference
                : pspReference.Length > 0 ? pspReference
                : null;

            return PaymentWebhookResult.FromStatus(new PaymentResponse { PaymentUniqueId = paymentUniqueId, PaymentStatus = status, ResponseObject = Acknowledgement });
        }
    }

    /// <summary>
    /// Returns the first <c>NotificationRequestItem</c> of the <c>notificationItems</c> envelope, or the
    /// root object when the item was posted without the envelope.
    /// </summary>
    private static JsonElement FindNotificationItem(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return default;

        if (!root.TryGetProperty("notificationItems", out var items))
            return root;

        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
            return default;

        var first = items[0];
        return first.ValueKind == JsonValueKind.Object && first.TryGetProperty("NotificationRequestItem", out var item)
            ? item
            : default;
    }

    /// <summary>
    /// Builds the text Adyen signs for a standard webhook item: eight fields in a fixed order, joined
    /// with a colon, an absent field contributing an empty string.
    /// </summary>
    private static string BuildSignedData(JsonElement item)
    {
        var amount = item.TryGetProperty("amount", out var a) ? a : default;
        return string.Join(':',
            Text(item, "pspReference"),
            Text(item, "originalReference"),
            Text(item, "merchantAccountCode"),
            Text(item, "merchantReference"),
            Text(amount, "value"),
            Text(amount, "currency"),
            Text(item, "eventCode"),
            Text(item, "success"));
    }

    /// <summary>Reads a scalar property as the text Adyen signed; an absent or non-scalar property is an empty string.</summary>
    private static string Text(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty,
        };
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla Adyen.</summary>
public static class AdyenProviderExtensions
{
    /// <summary>Rejestruje <see cref="AdyenProvider"/> i jego zależności w kontenerze DI.</summary>
    public static void RegisterAdyenProvider(this IServiceCollection services)
    {
        // Fail when the host starts, not on the first payment: these are the settings the provider cannot work without.
        services.AddOptions<AdyenServiceOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), $"'{AdyenServiceOptions.ConfigurationKey}:ApiKey' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.MerchantAccount), $"'{AdyenServiceOptions.ConfigurationKey}:MerchantAccount' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.NotificationHmacKey), $"'{AdyenServiceOptions.ConfigurationKey}:NotificationHmacKey' is not configured.")
            .ValidateOnStart();
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
