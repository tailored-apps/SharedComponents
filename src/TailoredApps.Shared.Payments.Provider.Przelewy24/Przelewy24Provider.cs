using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.Przelewy24;

// ─── Options ─────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja Przelewy24. Sekcja: <c>Payments:Providers:Przelewy24</c>.</summary>
public class Przelewy24ServiceOptions
{
    public static string ConfigurationKey => "Payments:Providers:Przelewy24";
    public int    MerchantId { get; set; }
    public int    PosId      { get; set; }
    public string ApiKey     { get; set; } = string.Empty;
    public string CrcKey     { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = "https://secure.przelewy24.pl";
    public string ReturnUrl  { get; set; } = string.Empty;
    public string NotifyUrl  { get; set; } = string.Empty;
}

// ─── Internal models ─────────────────────────────────────────────────────────

file class P24RegisterRequest
{
    [JsonPropertyName("merchantId")]   public int    MerchantId   { get; set; }
    [JsonPropertyName("posId")]        public int    PosId        { get; set; }
    [JsonPropertyName("sessionId")]    public string SessionId    { get; set; } = string.Empty;
    [JsonPropertyName("amount")]       public long   Amount       { get; set; }
    [JsonPropertyName("currency")]     public string Currency     { get; set; } = string.Empty;
    [JsonPropertyName("description")]  public string Description  { get; set; } = string.Empty;
    [JsonPropertyName("email")]        public string Email        { get; set; } = string.Empty;
    [JsonPropertyName("urlReturn")]    public string UrlReturn    { get; set; } = string.Empty;
    [JsonPropertyName("urlStatus")]    public string UrlStatus    { get; set; } = string.Empty;
    [JsonPropertyName("sign")]         public string Sign         { get; set; } = string.Empty;
    [JsonPropertyName("encoding")]     public string Encoding     { get; set; } = "UTF-8";
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
    [JsonPropertyName("merchantId")]  public int    MerchantId  { get; set; }
    [JsonPropertyName("posId")]       public int    PosId       { get; set; }
    [JsonPropertyName("sessionId")]   public string SessionId   { get; set; } = string.Empty;
    [JsonPropertyName("amount")]      public long   Amount      { get; set; }
    [JsonPropertyName("currency")]    public string Currency    { get; set; } = string.Empty;
    [JsonPropertyName("orderId")]     public int    OrderId     { get; set; }
    [JsonPropertyName("sign")]        public string Sign        { get; set; } = string.Empty;
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad Przelewy24 REST API.</summary>
public interface IPrzelewy24ServiceCaller
{
    Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId);
    Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId);
    string ComputeSign(string sessionId, int merchantId, long amount, string currency);
    bool VerifyNotification(string body);
}

// ─── Caller ───────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPrzelewy24ServiceCaller"/>.</summary>
public class Przelewy24ServiceCaller : IPrzelewy24ServiceCaller
{
    private readonly Przelewy24ServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

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

    public async Task<(string? token, string? error)> RegisterTransactionAsync(PaymentRequest request, string sessionId)
    {
        using var client = CreateClient();
        var amount = (long)(request.Amount * 100);
        var sign = ComputeSign(sessionId, options.MerchantId, amount, request.Currency);

        var body = new P24RegisterRequest
        {
            MerchantId  = options.MerchantId,
            PosId       = options.PosId,
            SessionId   = sessionId,
            Amount      = amount,
            Currency    = request.Currency.ToUpperInvariant(),
            Description = request.Title ?? request.Description ?? "Order",
            Email       = request.Email ?? string.Empty,
            UrlReturn   = options.ReturnUrl,
            UrlStatus   = options.NotifyUrl,
            Sign        = sign,
        };

        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/api/v1/transaction/register", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<P24RegisterResponse>(json);
        return (result?.Data?.Token, response.IsSuccessStatusCode ? null : json);
    }

    public async Task<PaymentStatusEnum> VerifyTransactionAsync(string sessionId, long amount, string currency, int orderId)
    {
        using var client = CreateClient();
        var sign = ComputeVerifySign(sessionId, orderId, options.MerchantId, amount, currency);
        var body = new P24VerifyRequest
        {
            MerchantId = options.MerchantId,
            PosId      = options.PosId,
            SessionId  = sessionId,
            Amount     = amount,
            Currency   = currency.ToUpperInvariant(),
            OrderId    = orderId,
            Sign       = sign,
        };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"{options.ServiceUrl}/api/v1/transaction/verify", content);
        return response.IsSuccessStatusCode ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected;
    }

    public string ComputeSign(string sessionId, int merchantId, long amount, string currency)
    {
        var json = JsonSerializer.Serialize(new { sessionId, merchantId, amount, currency, crc = options.CrcKey });
        var bytes = SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string ComputeVerifySign(string sessionId, int orderId, int merchantId, long amount, string currency)
    {
        var json = JsonSerializer.Serialize(new { sessionId, orderId, merchantId, amount, currency, crc = options.CrcKey });
        var bytes = SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyNotification(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("sign", out var signEl)) return false;
            var receivedSign = signEl.GetString() ?? string.Empty;

            doc.RootElement.TryGetProperty("sessionId", out var sid);
            doc.RootElement.TryGetProperty("orderId", out var oid);
            doc.RootElement.TryGetProperty("merchantId", out var mid);
            doc.RootElement.TryGetProperty("amount", out var amt);
            doc.RootElement.TryGetProperty("currency", out var cur);

            var json = JsonSerializer.Serialize(new
            {
                sessionId  = sid.GetString(),
                orderId    = oid.GetInt32(),
                merchantId = mid.GetInt32(),
                amount     = amt.GetInt64(),
                currency   = cur.GetString(),
                crc        = options.CrcKey,
            });
            var expected = Convert.ToHexString(SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
            return string.Equals(expected, receivedSign, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Przelewy24.</summary>
public class Przelewy24Provider : IPaymentProvider
{
    private readonly IPrzelewy24ServiceCaller caller;
    private readonly Przelewy24ServiceOptions options;

    public Przelewy24Provider(IPrzelewy24ServiceCaller caller, IOptions<Przelewy24ServiceOptions> options)
    {
        this.caller  = caller;
        this.options = options.Value;
    }

    public string Key         => "Przelewy24";
    public string Name        => "Przelewy24";
    public string Description => "Operator płatności online Przelewy24 — przelewy, BLIK, karty.";
    public string Url         => "https://przelewy24.pl";

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

    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var (token, error) = await caller.RegisterTransactionAsync(request, sessionId);

        if (token is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = error };

        return new PaymentResponse
        {
            PaymentUniqueId = sessionId,
            RedirectUrl     = $"{options.ServiceUrl}/trnRequest/{token}",
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    public Task<PaymentResponse> GetStatus(string paymentId)
        => Task.FromResult(new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = PaymentStatusEnum.Processing });

    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        if (!caller.VerifyNotification(body))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        PaymentStatusEnum status;
        try
        {
            var doc = JsonDocument.Parse(body);
            status = doc.RootElement.TryGetProperty("error", out _)
                ? PaymentStatusEnum.Rejected
                : PaymentStatusEnum.Finished;
        }
        catch { status = PaymentStatusEnum.Processing; }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla Przelewy24.</summary>
public static class Przelewy24ProviderExtensions
{
    public static void RegisterPrzelewy24Provider(this IServiceCollection services)
    {
        services.AddOptions<Przelewy24ServiceOptions>();
        services.ConfigureOptions<Przelewy24ConfigureOptions>();
        services.AddHttpClient("Przelewy24");
        services.AddTransient<IPrzelewy24ServiceCaller, Przelewy24ServiceCaller>();
    }
}

/// <summary>Wczytuje opcje Przelewy24 z konfiguracji.</summary>
public class Przelewy24ConfigureOptions : IConfigureOptions<Przelewy24ServiceOptions>
{
    private readonly IConfiguration configuration;
    public Przelewy24ConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    public void Configure(Przelewy24ServiceOptions options)
    {
        var s = configuration.GetSection(Przelewy24ServiceOptions.ConfigurationKey).Get<Przelewy24ServiceOptions>();
        if (s is null) return;
        options.MerchantId = s.MerchantId;
        options.PosId      = s.PosId;
        options.ApiKey     = s.ApiKey;
        options.CrcKey     = s.CrcKey;
        options.ServiceUrl = s.ServiceUrl;
        options.ReturnUrl  = s.ReturnUrl;
        options.NotifyUrl  = s.NotifyUrl;
    }
}
