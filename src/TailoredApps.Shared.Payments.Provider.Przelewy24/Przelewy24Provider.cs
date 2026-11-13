using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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

internal sealed class P24RegisterRequest
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

internal sealed class P24RegisterResponse
{
    [JsonPropertyName("data")] public P24RegisterData? Data { get; set; }
}

internal sealed class P24RegisterData
{
    [JsonPropertyName("token")] public string? Token { get; set; }
}

internal sealed class P24VerifyRequest
{
    [JsonPropertyName("merchantId")] public int MerchantId { get; set; }
    [JsonPropertyName("posId")] public int PosId { get; set; }
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public long Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("orderId")] public int OrderId { get; set; }
    [JsonPropertyName("sign")] public string Sign { get; set; } = string.Empty;
}

// The payloads Przelewy24 signs. The signature is a hash of this JSON, so the order of the members is the
// order Przelewy24 documents and must not change.

internal sealed record P24RegisterSign(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("merchantId")] int MerchantId,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("crc")] string Crc);

internal sealed record P24VerifySign(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("orderId")] int OrderId,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("crc")] string Crc);

internal sealed record P24NotificationSign(
    [property: JsonPropertyName("merchantId")] long MerchantId,
    [property: JsonPropertyName("posId")] long PosId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("originAmount")] long OriginAmount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("orderId")] long OrderId,
    [property: JsonPropertyName("methodId")] long MethodId,
    [property: JsonPropertyName("statement")] string Statement,
    [property: JsonPropertyName("crc")] string Crc);

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad Przelewy24 REST API.</summary>
public interface IPrzelewy24ServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId, CancellationToken cancellationToken = default);
    /// <summary>Wywołanie API.</summary>
    Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId, CancellationToken cancellationToken = default);
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
    private static readonly Przelewy24JsonContext SignJson = new(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

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
    public async Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var amount = (long)(request.Amount * 100);
        var currency = (request.Currency ?? throw new ArgumentException("Currency is required.", nameof(request))).ToUpperInvariant();
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

        var content = new StringContent(JsonSerializer.Serialize(body, Przelewy24JsonContext.Default.P24RegisterRequest), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/api/v1/transaction/register", content, cancellationToken);
        // 5xx, 408 and 429 mean the gateway did not handle the call; a 4xx answer is a refusal reported below.
        await PaymentGatewayException.ThrowIfUnavailableAsync("Przelewy24", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize(json, Przelewy24JsonContext.Default.P24RegisterResponse);
        return (result?.Data?.Token, response.IsSuccessStatusCode ? null : json);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId, CancellationToken cancellationToken = default)
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
        var content = new StringContent(JsonSerializer.Serialize(body, Przelewy24JsonContext.Default.P24VerifyRequest), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"{options.ServiceUrl}/api/v1/transaction/verify", content, cancellationToken);

        // A 4xx answer is Przelewy24 refusing the verification; 5xx, 408 and 429 leave the transaction unverified.
        await PaymentGatewayException.ThrowIfUnavailableAsync("Przelewy24", response, cancellationToken);
        return response.IsSuccessStatusCode ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected;
    }

    /// <inheritdoc/>
    public string ComputeSign(string sessionId, int merchantId, long amount, string currency)
    {
        // Register sign: {"sessionId","merchantId","amount","currency","crc"} (P24 field order).
        var json = JsonSerializer.Serialize(new P24RegisterSign(sessionId, merchantId, amount, currency, options.CrcKey), SignJson.P24RegisterSign);
        return Sha384Hex(json);
    }

    /// <summary>
    /// Verify sign as documented by Przelewy24 for <c>PUT /api/v1/transaction/verify</c>:
    /// <c>{"sessionId","orderId","amount","currency","crc"}</c>.
    /// </summary>
    private string ComputeVerifySign(string sessionId, int orderId, long amount, string currency)
    {
        var json = JsonSerializer.Serialize(new P24VerifySign(sessionId, orderId, amount, currency, options.CrcKey), SignJson.P24VerifySign);
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

            var json = JsonSerializer.Serialize(new P24NotificationSign(
                GetInt64(root, "merchantId"),
                GetInt64(root, "posId"),
                GetString(root, "sessionId"),
                GetInt64(root, "amount"),
                GetInt64(root, "originAmount"),
                GetString(root, "currency"),
                GetInt64(root, "orderId"),
                GetInt64(root, "methodId"),
                GetString(root, "statement"),
                options.CrcKey), SignJson.P24NotificationSign);
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
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency, CancellationToken cancellationToken = default)
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
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var (token, error) = await caller.RegisterTransactionAsync(request, sessionId, cancellationToken);

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
    public Task<PaymentResponse> GetStatus(string paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = PaymentStatusEnum.Processing });

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var body = request.Body ?? string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
        };

        return await ProcessNotificationAsync(payload, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload, CancellationToken cancellationToken = default)
        => (await ProcessNotificationAsync(payload, cancellationToken)).ToPaymentResponse();

    private async Task<PaymentWebhookResult> ProcessNotificationAsync(TransactionStatusChangePayload payload, CancellationToken cancellationToken)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        if (!caller.VerifyNotification(body))
            return PaymentWebhookResult.Reject(PaymentWebhookRejection.InvalidSignature, "Invalid signature");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("sessionId", out var sid) ||
                !root.TryGetProperty("amount", out var amt) ||
                !root.TryGetProperty("currency", out var cur) ||
                !root.TryGetProperty("orderId", out var oid))
                return PaymentWebhookResult.Reject(PaymentWebhookRejection.MalformedPayload, "Missing fields");

            var sessionId = sid.GetString()!;
            var status = await caller.VerifyTransactionAsync(
                sessionId,
                amt.GetInt64(),
                cur.GetString()!,
                oid.GetInt32(), cancellationToken);

            return PaymentWebhookResult.FromStatus(new PaymentResponse { PaymentUniqueId = sessionId, PaymentStatus = status, ResponseObject = "OK" });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PaymentGatewayException exception)
        {
            // The transaction could not be verified now. Rejecting makes Przelewy24 send the notification again.
            return PaymentWebhookResult.Reject(PaymentWebhookRejection.ProcessingFailed, exception.Message);
        }
        catch
        {
            return PaymentWebhookResult.Reject(PaymentWebhookRejection.ProcessingFailed, "Parse error");
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
        // Fail when the host starts, not on the first payment: these are the settings the provider cannot work without.
        services.AddOptions<Przelewy24ServiceOptions>()
            .Validate(o => o.MerchantId > 0, $"'{Przelewy24ServiceOptions.ConfigurationKey}:MerchantId' is not configured.")
            .Validate(o => o.PosId > 0, $"'{Przelewy24ServiceOptions.ConfigurationKey}:PosId' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), $"'{Przelewy24ServiceOptions.ConfigurationKey}:ApiKey' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.CrcKey), $"'{Przelewy24ServiceOptions.ConfigurationKey}:CrcKey' is not configured.")
            .ValidateOnStart();
        services.ConfigureOptions<Przelewy24ConfigureOptions>();
        services.AddHttpClient("Przelewy24");
        services.AddTransient<IPrzelewy24ServiceCaller, Przelewy24ServiceCaller>();
        services.AddTransient<Przelewy24Provider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<Przelewy24Provider>());
    }

    /// <summary>
    /// Registers the Przelewy24 provider with its dependencies and adds it to the payment service, so that
    /// <see cref="IPaymentService"/> can route to it. This is the one call a host needs for this provider:
    /// <c>builder.Services.AddPayments().RegisterPrzelewy24Provider();</c>
    /// </summary>
    /// <param name="builder">The builder returned by <c>AddPayments()</c>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IPaymentOptionsBuilder RegisterPrzelewy24Provider(this IPaymentOptionsBuilder builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RegisterPrzelewy24Provider();

        // A host that also calls RegisterPaymentProvider<Przelewy24Provider>() must not end up with the provider twice.
        foreach (var registered in builder.Services)
        {
            if (registered.ServiceType == typeof(IPaymentProvider) && registered.ImplementationType == typeof(Przelewy24Provider))
                return builder;
        }

        return builder.RegisterPaymentProvider<Przelewy24Provider>();
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

/// <summary>
/// JSON metadata for the requests and answers of the Przelewy24 API, generated at compile time: serialization
/// uses no reflection, so it survives trimming and works under NativeAOT.
/// </summary>
[JsonSerializable(typeof(P24RegisterRequest))]
[JsonSerializable(typeof(P24RegisterResponse))]
[JsonSerializable(typeof(P24VerifyRequest))]
[JsonSerializable(typeof(P24RegisterSign))]
[JsonSerializable(typeof(P24VerifySign))]
[JsonSerializable(typeof(P24NotificationSign))]
internal sealed partial class Przelewy24JsonContext : JsonSerializerContext;
