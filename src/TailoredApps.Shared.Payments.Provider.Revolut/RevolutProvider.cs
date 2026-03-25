using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.Revolut;

/// <summary>Konfiguracja Revolut. Sekcja: <c>Payments:Providers:Revolut</c>.</summary>
public class RevolutServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Revolut";
    /// <summary>ApiKey.</summary>
    public string ApiKey        { get; set; } = string.Empty;
    /// <summary>ApiUrl.</summary>
    public string ApiUrl        { get; set; } = "https://merchant.revolut.com/api";
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl     { get; set; } = string.Empty;
    /// <summary>WebhookSecret.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

file class RevolutOrderRequest
{
    [JsonPropertyName("amount")]                   public long    Amount      { get; set; }
    [JsonPropertyName("currency")]                 public string  Currency    { get; set; } = string.Empty;
    [JsonPropertyName("description")]              public string? Description { get; set; }
    [JsonPropertyName("merchant_order_ext_ref")]   public string? ExternalRef { get; set; }
    [JsonPropertyName("email")]                    public string? Email       { get; set; }
}

file class RevolutOrderResponse
{
    [JsonPropertyName("id")]           public string? Id          { get; set; }
    [JsonPropertyName("checkout_url")] public string? CheckoutUrl { get; set; }
    [JsonPropertyName("state")]        public string? State       { get; set; }
}

/// <summary>Abstrakcja nad Revolut Merchant API.</summary>
public interface IRevolutServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<(string? id, string? checkoutUrl)> CreateOrderAsync(PaymentRequest request);
    /// <summary>Wywołanie API.</summary>
    Task<(string? state, string? id)> GetOrderAsync(string orderId);
    /// <summary>Weryfikuje podpis powiadomienia.</summary>
    bool VerifyWebhookSignature(string payload, string timestamp, string signature);
}

/// <summary>Implementacja <see cref="IRevolutServiceCaller"/>.</summary>
public class RevolutServiceCaller : IRevolutServiceCaller
{
    private readonly RevolutServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję callera.</summary>
    public RevolutServiceCaller(IOptions<RevolutServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Revolut");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <inheritdoc/>
    public async Task<(string? id, string? checkoutUrl)> CreateOrderAsync(PaymentRequest request)
    {
        using var client = CreateClient();
        var body = new RevolutOrderRequest
        {
            Amount      = (long)(request.Amount * 100),
            Currency    = request.Currency.ToUpperInvariant(),
            Description = request.Title ?? request.Description,
            ExternalRef = request.AdditionalData ?? Guid.NewGuid().ToString("N"),
            Email       = request.Email,
        };
        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ApiUrl}/1.0/orders", content);
        var json     = await response.Content.ReadAsStringAsync();
        var result   = JsonSerializer.Deserialize<RevolutOrderResponse>(json);
        return (result?.Id, result?.CheckoutUrl);
    }

    /// <inheritdoc/>
    public async Task<(string? state, string? id)> GetOrderAsync(string orderId)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{options.ApiUrl}/1.0/orders/{orderId}");
        if (!response.IsSuccessStatusCode) return (null, orderId);
        var json   = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RevolutOrderResponse>(json);
        return (result?.State, result?.Id);
    }

    /// <summary>
    /// Weryfikuje podpis webhooka Revolut.
    /// Format: HMAC-SHA256("v1:{timestamp}.{payload}", webhookSecret).
    /// Nagłówek Revolut-Signature: v1=&lt;hex&gt;
    /// </summary>
    public bool VerifyWebhookSignature(string payload, string timestamp, string signature)
    {
        var signedPayload = $"v1:{timestamp}.{payload}";
        var keyBytes      = Encoding.UTF8.GetBytes(options.WebhookSecret);
        var dataBytes     = Encoding.UTF8.GetBytes(signedPayload);
        var computed      = Convert.ToHexString(HMACSHA256.HashData(keyBytes, dataBytes)).ToLowerInvariant();
        var receivedHex   = signature.StartsWith("v1=") ? signature.Substring(3) : signature;
        return string.Equals(computed, receivedHex, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Revolut.</summary>
public class RevolutProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IRevolutServiceCaller caller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public RevolutProvider(IRevolutServiceCaller caller) => this.caller = caller;

    public string Key         => "Revolut";
    public string Name        => "Revolut";
    /// <inheritdoc/>
    public string Description => "Globalny operator płatności Revolut — karty, Revolut Pay.";
    public string Url         => "https://revolut.com/business";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "card",        Name = "Karta płatnicza", Description = "Visa, Mastercard, Amex", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "revolut_pay", Name = "Revolut Pay",     Description = "Płatność Revolut Pay",   PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var (id, checkoutUrl) = await caller.CreateOrderAsync(request);
        if (id is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "API error" };
        return new PaymentResponse
        {
            PaymentUniqueId = id,
            RedirectUrl     = checkoutUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var (state, _) = await caller.GetOrderAsync(paymentId);
        var status = state switch
        {
            "completed"  => PaymentStatusEnum.Finished,
            "processing" => PaymentStatusEnum.Processing,
            "pending"    => PaymentStatusEnum.Processing,
            "authorised" => PaymentStatusEnum.Processing,
            "failed"     => PaymentStatusEnum.Rejected,
            "cancelled"  => PaymentStatusEnum.Rejected,
            _            => PaymentStatusEnum.Created,
        };
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var body      = request.Body ?? string.Empty;
        var timestamp = request.Headers.TryGetValue("Revolut-Request-Timestamp", out var t) ? t.ToString() : string.Empty;
        var signature = request.Headers.TryGetValue("Revolut-Signature",         out var s) ? s.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload         = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "Revolut-Request-Timestamp", timestamp },
                { "Revolut-Signature",         signature },
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
        var body      = payload.Payload?.ToString() ?? string.Empty;
        var timestamp = payload.QueryParameters.TryGetValue("Revolut-Request-Timestamp", out var t) ? t.ToString() : string.Empty;
        var signature = payload.QueryParameters.TryGetValue("Revolut-Signature", out var s) ? s.ToString() : string.Empty;

        if (!caller.VerifyWebhookSignature(body, timestamp, signature))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("event", out var ev))
                status = ev.GetString() switch
                {
                    "ORDER_COMPLETED"         => PaymentStatusEnum.Finished,
                    "ORDER_AUTHORISED"        => PaymentStatusEnum.Processing,
                    "ORDER_CANCELLED"         => PaymentStatusEnum.Rejected,
                    "ORDER_PAYMENT_DECLINED"  => PaymentStatusEnum.Rejected,
                    "PAYMENT_DECLINED"        => PaymentStatusEnum.Rejected,
                    _                         => PaymentStatusEnum.Processing,
                };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

/// <summary>Rozszerzenia DI dla Revolut.</summary>
public static class RevolutProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterRevolutProvider(this IServiceCollection services)
    {
        services.AddOptions<RevolutServiceOptions>();
        services.ConfigureOptions<RevolutConfigureOptions>();
        services.AddHttpClient("Revolut");
        services.AddTransient<IRevolutServiceCaller, RevolutServiceCaller>();
        services.AddTransient<RevolutProvider>();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<RevolutProvider>());
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<RevolutProvider>());
    }
}

/// <summary>Wczytuje opcje Revolut z konfiguracji.</summary>
public class RevolutConfigureOptions : IConfigureOptions<RevolutServiceOptions>
{
    private readonly IConfiguration configuration;
    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public RevolutConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    /// <inheritdoc/>
    public void Configure(RevolutServiceOptions options)
    {
        var s = configuration.GetSection(RevolutServiceOptions.ConfigurationKey).Get<RevolutServiceOptions>();
        if (s is null) return;
        options.ApiKey        = s.ApiKey;
        options.ApiUrl        = s.ApiUrl;
        options.ReturnUrl     = s.ReturnUrl;
        options.WebhookSecret = s.WebhookSecret;
    }
}
