using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.PayU;

// ─── Options ────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja providera PayU. Sekcja: <c>Payments:Providers:PayU</c>.</summary>
public class PayUServiceOptions
{
    public static string ConfigurationKey => "Payments:Providers:PayU";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>POS ID (merchantPosId) z panelu PayU.</summary>
    public string PosId { get; set; } = string.Empty;
    /// <summary>Klucz MD5 do weryfikacji powiadomień.</summary>
    public string SignatureKey { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = "https://secure.payu.com";
    public string NotifyUrl { get; set; } = string.Empty;
    public string ContinueUrl { get; set; } = string.Empty;
}

// ─── Internal models ─────────────────────────────────────────────────────────

file class PayUTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")]   public string TokenType { get; set; } = string.Empty;
}

file class PayUOrderRequest
{
    [JsonPropertyName("merchantPosId")]  public string MerchantPosId { get; set; } = string.Empty;
    [JsonPropertyName("description")]    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("currencyCode")]   public string CurrencyCode { get; set; } = string.Empty;
    [JsonPropertyName("totalAmount")]    public long TotalAmount { get; set; }
    [JsonPropertyName("notifyUrl")]      public string NotifyUrl { get; set; } = string.Empty;
    [JsonPropertyName("continueUrl")]    public string ContinueUrl { get; set; } = string.Empty;
    [JsonPropertyName("customerIp")]     public string CustomerIp { get; set; } = "127.0.0.1";
    [JsonPropertyName("buyer")]          public PayUBuyer? Buyer { get; set; }
    [JsonPropertyName("products")]       public List<PayUProduct> Products { get; set; } = [];
    [JsonPropertyName("payMethods")]     public PayUPayMethods? PayMethods { get; set; }
}

file class PayUBuyer
{
    [JsonPropertyName("email")]     public string Email { get; set; } = string.Empty;
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("lastName")]  public string LastName { get; set; } = string.Empty;
}

file class PayUProduct
{
    [JsonPropertyName("name")]      public string Name { get; set; } = string.Empty;
    [JsonPropertyName("unitPrice")] public long UnitPrice { get; set; }
    [JsonPropertyName("quantity")]  public int Quantity { get; set; } = 1;
}

file class PayUPayMethods
{
    [JsonPropertyName("payMethod")] public PayUPayMethod? PayMethod { get; set; }
}

file class PayUPayMethod
{
    [JsonPropertyName("type")]  public string Type { get; set; } = "PBL";
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}

file class PayUOrderResponse
{
    [JsonPropertyName("orderId")]     public string? OrderId { get; set; }
    [JsonPropertyName("redirectUri")] public string? RedirectUri { get; set; }
    [JsonPropertyName("status")]      public PayUStatus? Status { get; set; }
}

file class PayUStatusResponse
{
    [JsonPropertyName("orders")] public List<PayUOrder>? Orders { get; set; }
}

file class PayUOrder
{
    [JsonPropertyName("orderId")] public string? OrderId { get; set; }
    [JsonPropertyName("status")]  public string? Status { get; set; }
}

file class PayUStatus
{
    [JsonPropertyName("statusCode")] public string? StatusCode { get; set; }
}

// ─── Service Caller Interface ────────────────────────────────────────────────

/// <summary>Abstrakcja nad PayU REST API.</summary>
public interface IPayUServiceCaller
{
    Task<string> GetAccessTokenAsync();
    Task<PayUOrderResponse?> CreateOrderAsync(string accessToken, PaymentRequest request);
    Task<PaymentStatusEnum> GetOrderStatusAsync(string accessToken, string orderId);
    bool VerifyNotification(string body, string signatureHeader);
}

// ─── Service Caller ──────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPayUServiceCaller"/>.</summary>
public class PayUServiceCaller : IPayUServiceCaller
{
    private readonly PayUServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    public PayUServiceCaller(IOptions<PayUServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        using var client = httpClientFactory.CreateClient("PayU");
        var content = new FormUrlEncodedContent([
            new("grant_type",    "client_credentials"),
            new("client_id",    options.ClientId),
            new("client_secret", options.ClientSecret),
        ]);
        var response = await client.PostAsync($"{options.ServiceUrl}/pl/standard/user/oauth/authorize", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<PayUTokenResponse>(json);
        return token?.AccessToken ?? string.Empty;
    }

    public async Task<PayUOrderResponse?> CreateOrderAsync(string accessToken, PaymentRequest request)
    {
        using var client = httpClientFactory.CreateClient("PayU");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var order = new PayUOrderRequest
        {
            MerchantPosId = options.PosId,
            Description   = request.Title ?? request.Description ?? "Order",
            CurrencyCode  = request.Currency.ToUpperInvariant(),
            TotalAmount   = (long)(request.Amount * 100),
            NotifyUrl     = options.NotifyUrl,
            ContinueUrl   = options.ContinueUrl,
            Buyer = new PayUBuyer
            {
                Email     = request.Email ?? string.Empty,
                FirstName = request.FirstName ?? string.Empty,
                LastName  = request.Surname ?? string.Empty,
            },
            Products = [new PayUProduct { Name = request.Title ?? "Product", UnitPrice = (long)(request.Amount * 100) }],
        };

        if (!string.IsNullOrWhiteSpace(request.PaymentChannel))
            order.PayMethods = new PayUPayMethods { PayMethod = new PayUPayMethod { Value = request.PaymentChannel } };

        var json = JsonSerializer.Serialize(order);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // PayU zwraca 302 redirect — nie podążaj za nim
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var redirectClient = new HttpClient(handler);
        redirectClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        redirectClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await redirectClient.PostAsync($"{options.ServiceUrl}/api/v2_1/orders", content);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PayUOrderResponse>(responseJson);
    }

    public async Task<PaymentStatusEnum> GetOrderStatusAsync(string accessToken, string orderId)
    {
        using var client = httpClientFactory.CreateClient("PayU");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync($"{options.ServiceUrl}/api/v2_1/orders/{orderId}");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<PayUStatusResponse>(json);
        return status?.Orders?.FirstOrDefault()?.Status switch
        {
            "COMPLETED" => PaymentStatusEnum.Finished,
            "PENDING"   => PaymentStatusEnum.Processing,
            "WAITING_FOR_CONFIRMATION" => PaymentStatusEnum.Processing,
            "CANCELED"  => PaymentStatusEnum.Rejected,
            _           => PaymentStatusEnum.Created,
        };
    }

    public bool VerifyNotification(string body, string signatureHeader)
    {
        // Parsuj: signature=<base64>;algorithm=MD5;sender=PAYU
        var parts = signatureHeader.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        if (!parts.TryGetValue("signature", out var signature)) return false;

        var algorithm = parts.GetValueOrDefault("algorithm", "MD5");
        var input = body + options.SignatureKey;

        string computed;
        if (algorithm.Equals("SHA", StringComparison.OrdinalIgnoreCase) || algorithm.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            computed = Convert.ToHexString(bytes).ToLowerInvariant();
        }
        else
        {
            var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
            computed = Convert.ToHexString(bytes).ToLowerInvariant();
        }

        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }
}

// ─── Provider ────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla PayU.</summary>
public class PayUProvider : IPaymentProvider
{
    private readonly IPayUServiceCaller caller;

    public PayUProvider(IPayUServiceCaller caller) => this.caller = caller;

    public string Key         => "PayU";
    public string Name        => "PayU";
    public string Description => "Operator płatności online PayU — BLIK, karty, przelewy.";
    public string Url         => "https://payu.com";

    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "c",    Name = "Karta płatnicza", Description = "Visa, Mastercard",       PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "BLIK", Name = "BLIK",            Description = "Szybka płatność BLIK",   PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "m",    Name = "Przelew online",  Description = "Przelew bankowy online", PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var token = await caller.GetAccessTokenAsync();
        var order = await caller.CreateOrderAsync(token, request);
        return new PaymentResponse
        {
            PaymentUniqueId = order?.OrderId,
            RedirectUrl     = order?.RedirectUri,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var token = await caller.GetAccessTokenAsync();
        var status = await caller.GetOrderStatusAsync(token, paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    public Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
    {
        var body = payload.Payload?.ToString() ?? string.Empty;
        var sig  = payload.QueryParameters.TryGetValue("OpenPayU-Signature", out var s) ? s.ToString() : string.Empty;
        var valid = caller.VerifyNotification(body, sig);
        if (!valid)
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        // Parsuj status z body (JSON)
        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("order", out var order) &&
                order.TryGetProperty("status", out var s2))
            {
                status = s2.GetString() switch
                {
                    "COMPLETED" => PaymentStatusEnum.Finished,
                    "CANCELED"  => PaymentStatusEnum.Rejected,
                    _           => PaymentStatusEnum.Processing,
                };
            }
        }
        catch { /* ignore parse errors */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

// ─── DI Extensions ───────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla PayU.</summary>
public static class PayUProviderExtensions
{
    public static void RegisterPayUProvider(this IServiceCollection services)
    {
        services.AddOptions<PayUServiceOptions>();
        services.ConfigureOptions<PayUConfigureOptions>();
        services.AddHttpClient("PayU");
        services.AddTransient<IPayUServiceCaller, PayUServiceCaller>();
    }
}

/// <summary>Wczytuje opcje PayU z konfiguracji.</summary>
public class PayUConfigureOptions : IConfigureOptions<PayUServiceOptions>
{
    private readonly IConfiguration configuration;
    public PayUConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    public void Configure(PayUServiceOptions options)
    {
        var section = configuration.GetSection(PayUServiceOptions.ConfigurationKey).Get<PayUServiceOptions>();
        if (section is null) return;
        options.ClientId     = section.ClientId;
        options.ClientSecret = section.ClientSecret;
        options.PosId        = section.PosId;
        options.SignatureKey  = section.SignatureKey;
        options.ServiceUrl   = section.ServiceUrl;
        options.NotifyUrl    = section.NotifyUrl;
        options.ContinueUrl  = section.ContinueUrl;
    }
}
