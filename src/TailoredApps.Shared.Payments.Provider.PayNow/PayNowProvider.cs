using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.PayNow;

/// <summary>Konfiguracja PayNow. Sekcja: <c>Payments:Providers:PayNow</c>.</summary>
public class PayNowServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:PayNow";
    /// <summary>ApiKey.</summary>
    public string ApiKey       { get; set; } = string.Empty;
    /// <summary>SignatureKey.</summary>
    public string SignatureKey { get; set; } = string.Empty;
    /// <summary>Base URL of the PayNow API endpoint.</summary>
    public string ServiceUrl   { get; set; } = "https://api.paynow.pl";

    /// <summary>Alias for ServiceUrl — backwards compatibility.</summary>
    [Obsolete("Use ServiceUrl instead.")]
    public string ApiUrl { get => ServiceUrl; set => ServiceUrl = value; }
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl    { get; set; } = string.Empty;
    /// <summary>ContinueUrl.</summary>
    public string ContinueUrl  { get; set; } = string.Empty;
}

file class PayNowPaymentRequest
{
    [JsonPropertyName("amount")]      public long   Amount      { get; set; }
    [JsonPropertyName("currency")]    public string Currency    { get; set; } = "PLN";
    [JsonPropertyName("externalId")]  public string ExternalId  { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("buyer")]       public PayNowBuyer? Buyer { get; set; }
    [JsonPropertyName("continueUrl")] public string? ContinueUrl { get; set; }
    [JsonPropertyName("returnUrl")]   public string? ReturnUrl   { get; set; }
}

file class PayNowBuyer
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
}

file class PayNowPaymentResponse
{
    [JsonPropertyName("paymentId")]   public string? PaymentId   { get; set; }
    [JsonPropertyName("status")]      public string? Status      { get; set; }
    [JsonPropertyName("redirectUrl")] public string? RedirectUrl { get; set; }
}

file class PayNowStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
}

/// <summary>Abstrakcja nad PayNow REST API v2.</summary>
public interface IPayNowServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<(string? paymentId, string? redirectUrl)> CreatePaymentAsync(PaymentRequest request);
    /// <summary>Wywołanie API.</summary>
    Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId);
    /// <summary>Weryfikuje podpis powiadomienia.</summary>
    bool VerifySignature(string body, string signature);
}

/// <summary>Implementacja <see cref="IPayNowServiceCaller"/>.</summary>
public class PayNowServiceCaller : IPayNowServiceCaller
{
    private readonly PayNowServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję callera.</summary>
    public PayNowServiceCaller(IOptions<PayNowServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("PayNow");
        client.DefaultRequestHeaders.Add("Api-Key", options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <inheritdoc/>
    public async Task<(string? paymentId, string? redirectUrl)> CreatePaymentAsync(PaymentRequest request)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var body = new PayNowPaymentRequest
        {
            Amount      = (long)(request.Amount * 100),
            Currency    = request.Currency.ToUpperInvariant(),
            ExternalId  = request.AdditionalData ?? Guid.NewGuid().ToString("N"),
            Description = request.Title ?? request.Description ?? "Order",
            Buyer       = new PayNowBuyer { Email = request.Email ?? string.Empty },
            ContinueUrl = options.ContinueUrl,
            ReturnUrl   = options.ReturnUrl,
        };

        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/v2/payments", content);
        var json     = await response.Content.ReadAsStringAsync();
        var result   = JsonSerializer.Deserialize<PayNowPaymentResponse>(json);
        return (result?.PaymentId, result?.RedirectUrl);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{options.ServiceUrl}/v2/payments/{paymentId}/status");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json   = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<PayNowStatusResponse>(json)?.Status;
        return status switch
        {
            "CONFIRMED"  => PaymentStatusEnum.Finished,
            "PENDING"    => PaymentStatusEnum.Processing,
            "PROCESSING" => PaymentStatusEnum.Processing,
            "NEW"        => PaymentStatusEnum.Created,
            "ERROR"      => PaymentStatusEnum.Rejected,
            "REJECTED"   => PaymentStatusEnum.Rejected,
            "ABANDONED"  => PaymentStatusEnum.Rejected,
            _            => PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public bool VerifySignature(string body, string signature)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(options.SignatureKey);
        var dataBytes = Encoding.UTF8.GetBytes(body);
        var computed  = Convert.ToBase64String(HMACSHA256.HashData(keyBytes, dataBytes));
        return string.Equals(computed, signature, StringComparison.Ordinal);
    }
}

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla PayNow (mBank).</summary>
public class PayNowProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IPayNowServiceCaller caller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public PayNowProvider(IPayNowServiceCaller caller) => this.caller = caller;

    public string Key         => "PayNow";
    public string Name        => "PayNow";
    /// <inheritdoc/>
    public string Description => "Operator płatności PayNow (mBank) — BLIK, karty, przelewy.";
    public string Url         => "https://paynow.pl";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "BLIK",     Name = "BLIK",               Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "CARD",     Name = "Karta płatnicza",    Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "PBL",      Name = "Przelew bankowy",    Description = "Pay-by-link",      PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "TRANSFER", Name = "Przelew tradycyjny", Description = "Przelew bankowy",  PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var (paymentId, redirectUrl) = await caller.CreatePaymentAsync(request);
        if (paymentId is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "API error" };
        return new PaymentResponse
        {
            PaymentUniqueId = paymentId,
            RedirectUrl     = redirectUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
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
        var body      = request.Body ?? string.Empty;
        var signature = request.Headers.TryGetValue("Signature", out var s) ? s.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload         = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "Signature", signature },
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
        var sig  = payload.QueryParameters.TryGetValue("Signature", out var s) ? s.ToString() : string.Empty;

        if (!caller.VerifySignature(body, sig))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var st))
                status = st.GetString() switch
                {
                    "CONFIRMED"  => PaymentStatusEnum.Finished,
                    "ERROR"      => PaymentStatusEnum.Rejected,
                    "REJECTED"   => PaymentStatusEnum.Rejected,
                    "ABANDONED"  => PaymentStatusEnum.Rejected,
                    _            => PaymentStatusEnum.Processing,
                };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

/// <summary>Rozszerzenia DI dla PayNow.</summary>
public static class PayNowProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterPayNowProvider(this IServiceCollection services)
    {
        services.AddOptions<PayNowServiceOptions>();
        services.ConfigureOptions<PayNowConfigureOptions>();
        services.AddHttpClient("PayNow");
        services.AddTransient<IPayNowServiceCaller, PayNowServiceCaller>();
        services.AddTransient<PayNowProvider>();
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<PayNowProvider>());
    }
}

/// <summary>Wczytuje opcje PayNow z konfiguracji.</summary>
public class PayNowConfigureOptions : IConfigureOptions<PayNowServiceOptions>
{
    private readonly IConfiguration configuration;
    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public PayNowConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    /// <inheritdoc/>
    public void Configure(PayNowServiceOptions options)
    {
        var s = configuration.GetSection(PayNowServiceOptions.ConfigurationKey).Get<PayNowServiceOptions>();
        if (s is null) return;
        options.ApiKey       = s.ApiKey;
        options.SignatureKey = s.SignatureKey;
        options.ServiceUrl       = s.ServiceUrl;
        options.ReturnUrl    = s.ReturnUrl;
        options.ContinueUrl  = s.ContinueUrl;
    }
}
