using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Payments.Security;

namespace TailoredApps.Shared.Payments.Provider.Przelewy24;

// ─── Options ─────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja Przelewy24. Sekcja: <c>Payments:Providers:Przelewy24</c>.</summary>
public class Przelewy24ServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Przelewy24";
    /// <summary>Przelewy24 merchant identifier assigned in the merchant panel.</summary>
    public int MerchantId { get; set; }
    /// <summary>Point-of-sale (POS) identifier. Usually equals <see cref="MerchantId"/> for basic accounts.</summary>
    public int PosId { get; set; }
    /// <summary>ApiKey.</summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>CrcKey.</summary>
    public string CrcKey { get; set; } = string.Empty;
    /// <summary>ServiceUrl.</summary>
    public string ServiceUrl { get; set; } = "https://secure.przelewy24.pl";
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl { get; set; } = string.Empty;
    /// <summary>NotifyUrl.</summary>
    public string NotifyUrl { get; set; } = string.Empty;
}

// ─── Internal models ─────────────────────────────────────────────────────────

file class P24RegisterRequest
{
    [JsonPropertyName("merchantId")] public int MerchantId { get; set; }
    [JsonPropertyName("posId")] public int PosId { get; set; }
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public long Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    /// <inheritdoc/>
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("urlReturn")] public string UrlReturn { get; set; } = string.Empty;
    [JsonPropertyName("urlStatus")] public string UrlStatus { get; set; } = string.Empty;
    [JsonPropertyName("sign")] public string Sign { get; set; } = string.Empty;
    [JsonPropertyName("encoding")] public string Encoding { get; set; } = "UTF-8";
}

file class P24RegisterResponse
{
    [JsonPropertyName("data")] public P24RegisterData? Data { get; set; }
}

file class P24RegisterData
{
    [JsonPropertyName("token")] public string? Token { get; set; }
}

file class P24VerifyRequest
{
    [JsonPropertyName("merchantId")] public int MerchantId { get; set; }
    [JsonPropertyName("posId")] public int PosId { get; set; }
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public long Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("orderId")] public int OrderId { get; set; }
    [JsonPropertyName("sign")] public string Sign { get; set; } = string.Empty;
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad Przelewy24 REST API.</summary>
public interface IPrzelewy24ServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId);
    /// <summary>Wywołanie API.</summary>
    Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId);
    /// <summary>Computes the SHA-384 signature required by Przelewy24 for a transaction.</summary>
    /// <param name="sessionId">Unique session identifier for the transaction.</param>
    /// <param name="merchantId">Przelewy24 merchant identifier.</param>
    /// <param name="amount">Transaction amount in the smallest currency unit (e.g. grosz for PLN).</param>
    /// <param name="currency">ISO 4217 currency code (e.g. "PLN").</param>
    /// <returns>Hex-encoded SHA-384 hash of the sign string.</returns>
    string ComputeSign(string sessionId, int merchantId, long amount, string currency);
    /// <summary>Weryfikuje podpis powiadomienia.</summary>
    bool VerifyNotification(string body);
}

// ─── Caller ───────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPrzelewy24ServiceCaller"/>.</summary>
public class Przelewy24ServiceCaller : IPrzelewy24ServiceCaller
{
    private readonly Przelewy24ServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    // Przelewy24 computes its signatures over JSON with unescaped unicode and slashes
    // (PHP JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); mirror that here.
    private static readonly JsonSerializerOptions SignJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Inicjalizuje instancję callera.</summary>
    public Przelewy24ServiceCaller(IOptions<Przelewy24ServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Przelewy24");
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options.PosId}:{options.ApiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <inheritdoc/>
    public async Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId)
    {
        using var client = CreateClient();
        var amount = (long)(request.Amount * 100);
        var currency = request.Currency.ToUpperInvariant();
        var sign = ComputeSign(sessionId, options.MerchantId, amount, currency);

        var body = new P24RegisterRequest
        {
            MerchantId = options.MerchantId,
            PosId = options.PosId,
            SessionId = sessionId,
            Amount = amount,
            Currency = currency,
            Description = request.Title ?? request.Description ?? "Order",
            Email = request.Email ?? string.Empty,
            UrlReturn = options.ReturnUrl,
            UrlStatus = options.NotifyUrl,
            Sign = sign,
        };

        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/api/v1/transaction/register", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<P24RegisterResponse>(json);
        return (result?.Data?.Token, response.IsSuccessStatusCode ? null : json);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId)
    {
        using var client = CreateClient();
        var normalisedCurrency = currency.ToUpperInvariant();
        var sign = ComputeVerifySign(sessionId, orderId, amount, normalisedCurrency);
        var body = new P24VerifyRequest
        {
            MerchantId = options.MerchantId,
            PosId = options.PosId,
            SessionId = sessionId,
            Amount = amount,
            Currency = normalisedCurrency,
            OrderId = orderId,
            Sign = sign,
        };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"{options.ServiceUrl}/api/v1/transaction/verify", content);
        return response.IsSuccessStatusCode ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected;
    }

    /// <inheritdoc/>
    public string ComputeSign(string sessionId, int merchantId, long amount, string currency)
    {
        // Register sign: {"sessionId","merchantId","amount","currency","crc"} (P24 field order).
        var json = JsonSerializer.Serialize(new { sessionId, merchantId, amount, currency, crc = options.CrcKey }, SignJson);
        return Sha384Hex(json);
    }

    /// <summary>
    /// Verify sign as documented by Przelewy24 for <c>PUT /api/v1/transaction/verify</c>:
    /// <c>{"sessionId","orderId","amount","currency","crc"}</c>.
    /// </summary>
    private string ComputeVerifySign(string sessionId, int orderId, long amount, string currency)
    {
        var json = JsonSerializer.Serialize(new { sessionId, orderId, amount, currency, crc = options.CrcKey }, SignJson);
        return Sha384Hex(json);
    }

    private static string Sha384Hex(string json)
    {
        var bytes = SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The notification sign is SHA-384 over
    /// <c>{"merchantId","posId","sessionId","amount","originAmount","currency","orderId","methodId","statement","crc"}</c>
    /// in exactly that order, as documented by Przelewy24. Verification fails closed when the CRC key
    /// is not configured or the notification is missing the <c>sign</c> field.
    /// </remarks>
    public bool VerifyNotification(string body)
    {
        if (!WebhookSignature.IsSecretConfigured(options.CrcKey) || string.IsNullOrEmpty(body))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sign", out var signEl)) return false;
            var receivedSign = signEl.GetString() ?? string.Empty;

            var json = JsonSerializer.Serialize(new
            {
                merchantId = GetInt64(root, "merchantId"),
                posId = GetInt64(root, "posId"),
                sessionId = GetString(root, "sessionId"),
                amount = GetInt64(root, "amount"),
                originAmount = GetInt64(root, "originAmount"),
                currency = GetString(root, "currency"),
                orderId = GetInt64(root, "orderId"),
                methodId = GetInt64(root, "methodId"),
                statement = GetString(root, "statement"),
                crc = options.CrcKey,
            }, SignJson);
            var expected = Sha384Hex(json);
            return WebhookSignature.FixedTimeEqualsIgnoreCase(expected, receivedSign);
        }
        catch { return false; }
    }

    private static long GetInt64(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt64() : 0L;

    private static string GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : string.Empty;
}

// ─── Provider ─────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Przelewy24.</summary>
public class Przelewy24Provider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IPrzelewy24ServiceCaller caller;
    private readonly Przelewy24ServiceOptions options;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public Przelewy24Provider(IPrzelewy24ServiceCaller caller, IOptions<Przelewy24ServiceOptions> options)
    {
        this.caller = caller;
        this.options = options.Value;
    }

    /// <inheritdoc/>
    public string Key => "Przelewy24";
    /// <inheritdoc/>
    public string Name => "Przelewy24";
    /// <inheritdoc/>
    public string Description => "Operator płatności online Przelewy24 — przelewy, BLIK, karty.";
    /// <inheritdoc/>
    public string Url => "https://przelewy24.pl";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "online_transfer", Name = "Przelew online",  Description = "Wszystkie banki",      PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "blik",            Name = "BLIK",            Description = "Płatność BLIK",        PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "card",            Name = "Karta płatnicza", Description = "Visa, Mastercard",     PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var (token, error) = await caller.RegisterTransactionAsync(request, sessionId);

        if (token is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = error };

        return new PaymentResponse
        {
            PaymentUniqueId = sessionId,
            RedirectUrl = $"{options.ServiceUrl}/trnRequest/{token}",
            PaymentStatus = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public Task<PaymentResponse> GetStatus(string paymentId)
        => Task.FromResult(new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = PaymentStatusEnum.Processing });

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var body = request.Body ?? string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
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

        if (response.PaymentStatus == PaymentStatusEnum.Processing)
            return PaymentWebhookResult.Ignore("Non-actionable event");

        return PaymentWebhookResult.Ok(response);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        if (!caller.VerifyNotification(body))
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" };

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("sessionId", out var sid) ||
                !root.TryGetProperty("amount", out var amt) ||
                !root.TryGetProperty("currency", out var cur) ||
                !root.TryGetProperty("orderId", out var oid))
                return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Missing fields" };

            var sessionId = sid.GetString()!;
            var status = await caller.VerifyTransactionAsync(
                sessionId,
                amt.GetInt64(),
                cur.GetString()!,
                oid.GetInt32());

            return new PaymentResponse { PaymentUniqueId = sessionId, PaymentStatus = status, ResponseObject = "OK" };
        }
        catch
        {
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Parse error" };
        }
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla Przelewy24.</summary>
public static class Przelewy24ProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterPrzelewy24Provider(this IServiceCollection services)
    {
        services.AddOptions<Przelewy24ServiceOptions>();
        services.ConfigureOptions<Przelewy24ConfigureOptions>();
        services.AddHttpClient("Przelewy24");
        services.AddTransient<IPrzelewy24ServiceCaller, Przelewy24ServiceCaller>();
        services.AddTransient<Przelewy24Provider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<Przelewy24Provider>());
    }
}

/// <summary>Wczytuje opcje Przelewy24 z konfiguracji.</summary>
public class Przelewy24ConfigureOptions : IConfigureOptions<Przelewy24ServiceOptions>
{
    private readonly IConfiguration configuration;
    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public Przelewy24ConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    /// <inheritdoc/>
    public void Configure(Przelewy24ServiceOptions options)
    {
        var s = configuration.GetSection(Przelewy24ServiceOptions.ConfigurationKey).Get<Przelewy24ServiceOptions>();
        if (s is null) return;
        options.MerchantId = s.MerchantId;
        options.PosId = s.PosId;
        options.ApiKey = s.ApiKey;
        options.CrcKey = s.CrcKey;
        options.ServiceUrl = s.ServiceUrl;
        options.ReturnUrl = s.ReturnUrl;
        options.NotifyUrl = s.NotifyUrl;
    }
}
