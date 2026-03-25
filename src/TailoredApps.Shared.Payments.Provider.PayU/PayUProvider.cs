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

// ─── Options ─────────────────────────────────────────────────────────────────

/// <summary>Konfiguracja PayU REST API v2.1. Sekcja: <c>Payments:Providers:PayU</c>.</summary>
public class PayUServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:PayU";

    /// <summary>Identyfikator klienta OAuth (client_id).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Sekret klienta OAuth (client_secret).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Identyfikator POS (merchantPosId).</summary>
    public string PosId { get; set; } = string.Empty;

    /// <summary>Klucz do podpisu powiadomień (second key / signature key).</summary>
    public string SignatureKey { get; set; } = string.Empty;

    /// <summary>Bazowy URL API PayU (sandbox: https://secure.snd.payu.com).</summary>
    public string ServiceUrl { get; set; } = "https://secure.snd.payu.com";

    /// <summary>URL powiadomień o statusie transakcji.</summary>
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>URL powrotu po płatności.</summary>
    public string ContinueUrl { get; set; } = string.Empty;
}

// ─── Internal models ─────────────────────────────────────────────────────────

file class PayUTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")]   public string TokenType   { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")]   public int    ExpiresIn   { get; set; }
}

file class PayUOrderRequest
{
    [JsonPropertyName("notifyUrl")]    public string  NotifyUrl    { get; set; } = string.Empty;
    [JsonPropertyName("continueUrl")]  public string  ContinueUrl  { get; set; } = string.Empty;
    [JsonPropertyName("customerIp")]   public string  CustomerIp   { get; set; } = "127.0.0.1";
    [JsonPropertyName("merchantPosId")]public string  MerchantPosId { get; set; } = string.Empty;
    [JsonPropertyName("description")]  public string  Description  { get; set; } = string.Empty;
    [JsonPropertyName("currencyCode")] public string  CurrencyCode { get; set; } = string.Empty;
    [JsonPropertyName("totalAmount")]  public string  TotalAmount  { get; set; } = string.Empty;
    [JsonPropertyName("buyer")]        public PayUBuyer? Buyer     { get; set; }
    [JsonPropertyName("products")]     public List<PayUProduct> Products { get; set; } = [];
}

file class PayUBuyer
{
    [JsonPropertyName("email")]     public string Email     { get; set; } = string.Empty;
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("lastName")]  public string LastName  { get; set; } = string.Empty;
}

file class PayUProduct
{
    [JsonPropertyName("name")]      public string Name      { get; set; } = string.Empty;
    [JsonPropertyName("unitPrice")] public string UnitPrice { get; set; } = string.Empty;
    [JsonPropertyName("quantity")]  public string Quantity   { get; set; } = "1";
}

file class PayUOrderResponse
{
    [JsonPropertyName("status")]    public PayUStatus? Status   { get; set; }
    [JsonPropertyName("orderId")]   public string?     OrderId  { get; set; }
    [JsonPropertyName("redirectUri")] public string?   RedirectUri { get; set; }
}

file class PayUStatus
{
    [JsonPropertyName("statusCode")] public string? StatusCode { get; set; }
}

file class PayUStatusResponse
{
    [JsonPropertyName("orders")] public List<PayUOrderDetail>? Orders { get; set; }
}

file class PayUOrderDetail
{
    [JsonPropertyName("orderId")] public string? OrderId { get; set; }
    [JsonPropertyName("status")]  public string? Status  { get; set; }
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>Abstrakcja nad PayU REST API v2.1.</summary>
public interface IPayUServiceCaller
{
    /// <summary>Pobiera token OAuth (grant_type=client_credentials).</summary>
    Task<string> GetAccessTokenAsync();

    /// <summary>Tworzy zamówienie w PayU i zwraca orderId + redirectUri.</summary>
    Task<(string? orderId, string? redirectUri, string? error)> CreateOrderAsync(string token, PaymentRequest request);

    /// <summary>Pobiera status zamówienia po orderId.</summary>
    Task<PaymentStatusEnum> GetOrderStatusAsync(string token, string orderId);

    /// <summary>Weryfikuje podpis powiadomienia (OpenPayU-Signature header).</summary>
    bool VerifySignature(string body, string incomingSignature);
}

// ─── Caller ───────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPayUServiceCaller"/>.</summary>
public class PayUServiceCaller : IPayUServiceCaller
{
    private readonly PayUServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję <see cref="PayUServiceCaller"/>.</summary>
    public PayUServiceCaller(IOptions<PayUServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync()
    {
        using var client = httpClientFactory.CreateClient("PayU");
        var content = new FormUrlEncodedContent([
            new("grant_type",    "client_credentials"),
            new("client_id",     options.ClientId),
            new("client_secret", options.ClientSecret),
        ]);
        var response = await client.PostAsync($"{options.ServiceUrl}/pl/standard/user/oauth/authorize", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PayUTokenResponse>(json)?.AccessToken ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<(string? orderId, string? redirectUri, string? error)> CreateOrderAsync(string token, PaymentRequest request)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var amount = ((long)(request.Amount * 100)).ToString();
        var body = new PayUOrderRequest
        {
            NotifyUrl     = options.NotifyUrl,
            ContinueUrl   = options.ContinueUrl,
            MerchantPosId = options.PosId,
            Description   = request.Title ?? request.Description ?? "Order",
            CurrencyCode  = request.Currency.ToUpperInvariant(),
            TotalAmount   = amount,
            Buyer         = new PayUBuyer { Email = request.Email ?? string.Empty, FirstName = request.FirstName ?? string.Empty, LastName = request.Surname ?? string.Empty },
            Products      = [new PayUProduct { Name = request.Title ?? "Product", UnitPrice = amount }],
        };

        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/api/v2_1/orders", content);
        var json     = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.Found || response.StatusCode == System.Net.HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.ToString();
            var result   = JsonSerializer.Deserialize<PayUOrderResponse>(json);
            return (result?.OrderId, location ?? result?.RedirectUri, null);
        }

        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<PayUOrderResponse>(json);
            return (result?.OrderId, result?.RedirectUri, null);
        }

        return (null, null, json);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetOrderStatusAsync(string token, string orderId)
    {
        using var client = httpClientFactory.CreateClient("PayU");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync($"{options.ServiceUrl}/api/v2_1/orders/{orderId}");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json   = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PayUStatusResponse>(json);
        var status = result?.Orders?.FirstOrDefault()?.Status;
        return status switch
        {
            "COMPLETED"                => PaymentStatusEnum.Finished,
            "WAITING_FOR_CONFIRMATION" => PaymentStatusEnum.Processing,
            "PENDING"                  => PaymentStatusEnum.Processing,
            "CANCELED"                 => PaymentStatusEnum.Rejected,
            "REJECTED"                 => PaymentStatusEnum.Rejected,
            _                          => PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public bool VerifySignature(string body, string incomingSignature)
    {
        var parts = incomingSignature.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        if (!parts.TryGetValue("signature", out var receivedSig)) return false;
        var algorithm = parts.GetValueOrDefault("algorithm", "MD5");

        var data = body + options.SignatureKey;
        string computed;

        if (algorithm.Equals("SHA256", StringComparison.OrdinalIgnoreCase) || algorithm.Equals("SHA-256", StringComparison.OrdinalIgnoreCase))
            computed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        else
            computed = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

        return string.Equals(computed, receivedSig, StringComparison.OrdinalIgnoreCase);
    }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla PayU.</summary>
public class PayUProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IPayUServiceCaller caller;

    /// <summary>Inicjalizuje instancję <see cref="PayUProvider"/>.</summary>
    public PayUProvider(IPayUServiceCaller caller) => this.caller = caller;

    /// <inheritdoc/>
    public string Key => "PayU";

    /// <inheritdoc/>
    public string Name => "PayU";

    /// <inheritdoc/>
    public string Description => "Operator płatności PayU — przelewy, BLIK, karty, raty.";

    /// <inheritdoc/>
    public string Url => "https://payu.pl";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels = currency.ToUpperInvariant() switch
        {
            "PLN" =>
            [
                new PaymentChannel { Id = "blik",     Name = "BLIK",              Description = "BLIK",                PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "c",        Name = "Karta płatnicza",   Description = "Visa, Mastercard",    PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "o",        Name = "Przelew online",    Description = "Pekao, mBank, iPKO",  PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "ai",       Name = "Raty",              Description = "Raty PayU",           PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "ap",       Name = "Apple Pay",         Description = "Apple Pay",           PaymentModel = PaymentModel.OneTime },
                new PaymentChannel { Id = "jp",       Name = "Google Pay",        Description = "Google Pay",          PaymentModel = PaymentModel.OneTime },
            ],
            _ =>
            [
                new PaymentChannel { Id = "c",  Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            ],
        };
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var token = await caller.GetAccessTokenAsync();
        var (orderId, redirectUri, error) = await caller.CreateOrderAsync(token, request);

        if (orderId is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = error };

        return new PaymentResponse
        {
            PaymentUniqueId = orderId,
            RedirectUrl     = redirectUri,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var token  = await caller.GetAccessTokenAsync();
        var status = await caller.GetOrderStatusAsync(token, paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var body      = request.Body ?? string.Empty;
        var signature = request.Headers.TryGetValue("OpenPayU-Signature", out var s) ? s.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload         = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "OpenPayU-Signature", signature },
            },
        };

        var response = await TransactionStatusChange(payload);

        if (response.PaymentStatus == PaymentStatusEnum.Rejected)
        {
            var msg = response.ResponseObject?.ToString() ?? string.Empty;
            if (msg.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("hash",      StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("sign",      StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("hmac",      StringComparison.OrdinalIgnoreCase))
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
        var sig  = payload.QueryParameters.TryGetValue("OpenPayU-Signature", out var s) ? s.ToString() : string.Empty;

        if (!caller.VerifySignature(body, sig))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("order", out var orderEl) && orderEl.TryGetProperty("status", out var st))
                status = st.GetString() switch
                {
                    "COMPLETED" => PaymentStatusEnum.Finished,
                    "CANCELED"  => PaymentStatusEnum.Rejected,
                    "REJECTED"  => PaymentStatusEnum.Rejected,
                    _           => PaymentStatusEnum.Processing,
                };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

// ─── DI ───────────────────────────────────────────────────────────────────────

/// <summary>Rozszerzenia DI dla PayU.</summary>
public static class PayUProviderExtensions
{
    /// <summary>Rejestruje <see cref="PayUProvider"/> i jego zależności w kontenerze DI.</summary>
    public static void RegisterPayUProvider(this IServiceCollection services)
    {
        services.AddOptions<PayUServiceOptions>();
        services.ConfigureOptions<PayUConfigureOptions>();
        services.AddHttpClient("PayU");
        services.AddTransient<IPayUServiceCaller, PayUServiceCaller>();
        services.AddTransient<PayUProvider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<PayUProvider>());
    }
}

/// <summary>Wczytuje opcje PayU z konfiguracji aplikacji.</summary>
public class PayUConfigureOptions : IConfigureOptions<PayUServiceOptions>
{
    private readonly IConfiguration configuration;

    /// <summary>Inicjalizuje instancję <see cref="PayUConfigureOptions"/>.</summary>
    public PayUConfigureOptions(IConfiguration configuration) => this.configuration = configuration;

    /// <inheritdoc/>
    public void Configure(PayUServiceOptions options)
    {
        var s = configuration.GetSection(PayUServiceOptions.ConfigurationKey).Get<PayUServiceOptions>();
        if (s is null) return;
        options.ClientId     = s.ClientId;
        options.ClientSecret = s.ClientSecret;
        options.PosId        = s.PosId;
        options.SignatureKey = s.SignatureKey;
        options.ServiceUrl   = s.ServiceUrl;
        options.NotifyUrl    = s.NotifyUrl;
        options.ContinueUrl  = s.ContinueUrl;
    }
}
