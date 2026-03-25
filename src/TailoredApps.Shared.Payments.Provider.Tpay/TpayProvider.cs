using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.Tpay;

public class TpayServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Tpay";
    /// <summary>ClientId.</summary>
    public string ClientId     { get; set; } = string.Empty;
    /// <summary>ClientSecret.</summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>MerchantId.</summary>
    public string MerchantId   { get; set; } = string.Empty;
    /// <summary>Base URL of the Tpay API endpoint.</summary>
    public string ServiceUrl   { get; set; } = "https://api.tpay.com";

    /// <summary>Alias for <see cref="ServiceUrl"/> — kept for backwards compatibility.</summary>
    [Obsolete("Use ServiceUrl instead.")]
    public string ApiUrl
    {
        get => ServiceUrl;
        set => ServiceUrl = value;
    }
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl    { get; set; } = string.Empty;
    /// <summary>NotifyUrl.</summary>
    public string NotifyUrl    { get; set; } = string.Empty;
    /// <summary>SecurityCode.</summary>
    public string SecurityCode { get; set; } = string.Empty;
}

file class TpayTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}

file class TpayTransactionRequest
{
    [JsonPropertyName("amount")]            public decimal Amount        { get; set; }
    [JsonPropertyName("description")]       public string  Description   { get; set; } = string.Empty;
    [JsonPropertyName("hiddenDescription")] public string? HiddenDescription { get; set; }
    [JsonPropertyName("lang")]              public string  Lang          { get; set; } = "pl";
    [JsonPropertyName("pay")]               public TpayPay Pay           { get; set; } = new();
    [JsonPropertyName("payer")]             public TpayPayer Payer       { get; set; } = new();
    [JsonPropertyName("callbacks")]         public TpayCallbacks Callbacks { get; set; } = new();
}

file class TpayPay
{
    [JsonPropertyName("groupId")]  public int?    GroupId  { get; set; }
    [JsonPropertyName("channel")]  public string? Channel  { get; set; }
}

file class TpayPayer
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("name")]  public string Name  { get; set; } = string.Empty;
}

file class TpayCallbacks
{
    [JsonPropertyName("payerUrls")]    public TpayPayerUrls PayerUrls       { get; set; } = new();
    [JsonPropertyName("notification")] public TpayNotification Notification { get; set; } = new();
}

file class TpayPayerUrls
{
    [JsonPropertyName("success")] public string Success { get; set; } = string.Empty;
    [JsonPropertyName("error")]   public string Error   { get; set; } = string.Empty;
}

file class TpayNotification
{
    [JsonPropertyName("url")]   public string Url   { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
}

file class TpayTransactionResponse
{
    [JsonPropertyName("transactionId")]         public string? TransactionId { get; set; }
    [JsonPropertyName("transactionPaymentUrl")] public string? PaymentUrl    { get; set; }
    [JsonPropertyName("title")]                 public string? Title         { get; set; }
}

file class TpayStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
}

/// <summary>Abstrakcja nad Tpay REST API.</summary>
public interface ITpayServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<string> GetAccessTokenAsync();
    /// <summary>Wywołanie API.</summary>
    Task<(string? transactionId, string? paymentUrl)> CreateTransactionAsync(string token, PaymentRequest request);
    /// <summary>Wywołanie API.</summary>
    Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId);
    /// <summary>Weryfikuje podpis powiadomienia.</summary>
    bool VerifyNotification(string body, string signature);
}

/// <summary>Implementacja <see cref="ITpayServiceCaller"/>.</summary>
public class TpayServiceCaller : ITpayServiceCaller
{
    private readonly TpayServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję callera.</summary>
    public TpayServiceCaller(IOptions<TpayServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync()
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        var content = new FormUrlEncodedContent([
            new("grant_type",    "client_credentials"),
            new("client_id",     options.ClientId),
            new("client_secret", options.ClientSecret),
        ]);
        var response = await client.PostAsync($"{options.ServiceUrl}/oauth/auth", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TpayTokenResponse>(json)?.AccessToken ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<(string? transactionId, string? paymentUrl)> CreateTransactionAsync(string token, PaymentRequest request)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new TpayTransactionRequest
        {
            Amount      = request.Amount,
            Description = request.Title ?? request.Description ?? "Order",
            Payer       = new TpayPayer { Email = request.Email ?? string.Empty, Name = $"{request.FirstName} {request.Surname}".Trim() },
            Callbacks   = new TpayCallbacks
            {
                PayerUrls    = new TpayPayerUrls { Success = options.ReturnUrl, Error = options.ReturnUrl },
                Notification = new TpayNotification { Url = options.NotifyUrl, Email = request.Email ?? string.Empty },
            },
        };

        if (!string.IsNullOrWhiteSpace(request.PaymentChannel))
            body.Pay = new TpayPay { Channel = request.PaymentChannel };

        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/transactions", content);
        var json     = await response.Content.ReadAsStringAsync();
        var tx       = JsonSerializer.Deserialize<TpayTransactionResponse>(json);
        return (tx?.TransactionId, tx?.PaymentUrl);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync($"{options.ServiceUrl}/transactions/{transactionId}");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json   = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<TpayStatusResponse>(json)?.Status;
        return status switch
        {
            "correct"    => PaymentStatusEnum.Finished,
            "pending"    => PaymentStatusEnum.Processing,
            "error"      => PaymentStatusEnum.Rejected,
            "chargeback" => PaymentStatusEnum.Rejected,
            _            => PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public bool VerifyNotification(string body, string signature)
    {
        var input    = body + options.SecurityCode;
        var hash     = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Tpay.</summary>
public class TpayProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly ITpayServiceCaller caller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public TpayProvider(ITpayServiceCaller caller) => this.caller = caller;

    public string Key         => "Tpay";
    public string Name        => "Tpay";
    /// <inheritdoc/>
    public string Description => "Operator płatności Tpay — przelewy, BLIK, karty.";
    public string Url         => "https://tpay.com";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "blik", Name = "BLIK",            Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "card", Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "bank", Name = "Przelew online",  Description = "mTransfer, iPKO",  PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var token = await caller.GetAccessTokenAsync();
        var (transactionId, paymentUrl) = await caller.CreateTransactionAsync(token, request);
        if (transactionId is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "API error" };
        return new PaymentResponse
        {
            PaymentUniqueId = transactionId,
            RedirectUrl     = paymentUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var token  = await caller.GetAccessTokenAsync();
        var status = await caller.GetTransactionStatusAsync(token, paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var body      = request.Body ?? string.Empty;
        var signature = request.Headers.TryGetValue("X-Signature", out var s) ? s.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            Payload         = body,
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "X-Signature", signature },
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
        var sig  = payload.QueryParameters.TryGetValue("X-Signature", out var s) ? s.ToString() : string.Empty;

        if (!caller.VerifyNotification(body, sig))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid signature" });

        var status = PaymentStatusEnum.Processing;
        try
        {
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // Support both "status" (API v2) and "tr_status" (legacy webhook) fields
            if (root.TryGetProperty("status", out var st) || root.TryGetProperty("tr_status", out st))
                status = st.GetString() switch
                {
                    "paid"       => PaymentStatusEnum.Finished,
                    "correct"    => PaymentStatusEnum.Finished,
                    "TRUE"       => PaymentStatusEnum.Finished,
                    "pending"    => PaymentStatusEnum.Processing,
                    "error"      => PaymentStatusEnum.Rejected,
                    "chargeback" => PaymentStatusEnum.Rejected,
                    "FALSE"      => PaymentStatusEnum.Rejected,
                    _            => PaymentStatusEnum.Processing,
                };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

/// <summary>Rozszerzenia DI dla Tpay.</summary>
public static class TpayProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterTpayProvider(this IServiceCollection services)
    {
        services.AddOptions<TpayServiceOptions>();
        services.ConfigureOptions<TpayConfigureOptions>();
        services.AddHttpClient("Tpay");
        services.AddTransient<ITpayServiceCaller, TpayServiceCaller>();
        services.AddTransient<TpayProvider>();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<TpayProvider>());
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<TpayProvider>());
    }
}

/// <summary>Wczytuje opcje Tpay z konfiguracji.</summary>
public class TpayConfigureOptions : IConfigureOptions<TpayServiceOptions>
{
    private readonly IConfiguration configuration;
    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public TpayConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    /// <inheritdoc/>
    public void Configure(TpayServiceOptions options)
    {
        var s = configuration.GetSection(TpayServiceOptions.ConfigurationKey).Get<TpayServiceOptions>();
        if (s is null) return;
        options.ClientId     = s.ClientId;
        options.ClientSecret = s.ClientSecret;
        options.MerchantId   = s.MerchantId;
        options.ServiceUrl       = s.ServiceUrl;
        options.ReturnUrl    = s.ReturnUrl;
        options.NotifyUrl    = s.NotifyUrl;
        options.SecurityCode = s.SecurityCode;
    }
}
