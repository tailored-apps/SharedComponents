using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.HotPay;

/// <summary>Konfiguracja HotPay. Sekcja: <c>Payments:Providers:HotPay</c>.</summary>
public class HotPayServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:HotPay";
    /// <summary>SecretHash.</summary>
    public string SecretHash { get; set; } = string.Empty;
    /// <summary>ServiceUrl.</summary>
    public string ServiceUrl { get; set; } = "https://platnosci.hotpay.pl";
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl  { get; set; } = string.Empty;
    /// <summary>NotifyUrl.</summary>
    public string NotifyUrl  { get; set; } = string.Empty;
}

file class HotPayRequest
{
    [JsonPropertyName("SEKRET")]                  public string Secret      { get; set; } = string.Empty;
    [JsonPropertyName("KWOTA")]                   public string Amount      { get; set; } = string.Empty;
    [JsonPropertyName("NAZWA_USLUGI")]            public string ServiceName { get; set; } = string.Empty;
    [JsonPropertyName("IDENTYFIKATOR_PLATNOSCI")] public string PaymentId   { get; set; } = string.Empty;
    [JsonPropertyName("ADRES_WWW")]               public string ReturnUrl   { get; set; } = string.Empty;
    [JsonPropertyName("EMAIL")]                   public string? Email      { get; set; }
    [JsonPropertyName("HASH")]                    public string Hash        { get; set; } = string.Empty;
}

file class HotPayResponse
{
    [JsonPropertyName("STATUS")]        public string? Status      { get; set; }
    [JsonPropertyName("PRZEKIERUJ_DO")] public string? RedirectUrl { get; set; }
    [JsonPropertyName("ID_PLATNOSCI")]  public string? PaymentId   { get; set; }
}

/// <summary>Abstrakcja nad HotPay API.</summary>
public interface IHotPayServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<(string? paymentId, string? redirectUrl)> InitPaymentAsync(PaymentRequest request, string paymentId);
    /// <summary>Weryfikuje podpis powiadomienia.</summary>
    bool VerifyNotification(string hash, string kwota, string idPlatnosci, string status);
}

/// <summary>Implementacja <see cref="IHotPayServiceCaller"/>.</summary>
public class HotPayServiceCaller : IHotPayServiceCaller
{
    private readonly HotPayServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>Inicjalizuje instancję callera.</summary>
    public HotPayServiceCaller(IOptions<HotPayServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<(string? paymentId, string? redirectUrl)> InitPaymentAsync(PaymentRequest request, string paymentId)
    {
        var amount   = request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var hashData = $"{options.SecretHash};{amount};{request.Title ?? "Order"};{paymentId};{options.ReturnUrl}";
        var hash     = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashData))).ToLowerInvariant();

        var body = new HotPayRequest
        {
            Secret      = options.SecretHash,
            Amount      = amount,
            ServiceName = request.Title ?? request.Description ?? "Order",
            PaymentId   = paymentId,
            ReturnUrl   = options.ReturnUrl,
            Email       = request.Email,
            Hash        = hash,
        };

        using var client   = httpClientFactory.CreateClient("HotPay");
        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(options.ServiceUrl, content);
        var json     = await response.Content.ReadAsStringAsync();
        var result   = JsonSerializer.Deserialize<HotPayResponse>(json);
        return (result?.PaymentId ?? paymentId, result?.RedirectUrl);
    }

    /// <inheritdoc/>
    public bool VerifyNotification(string hash, string kwota, string idPlatnosci, string status)
    {
        var data     = $"{options.SecretHash};{kwota};{idPlatnosci};{status}";
        var computed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        return string.Equals(computed, hash, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla HotPay.</summary>
public class HotPayProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly IHotPayServiceCaller caller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public HotPayProvider(IHotPayServiceCaller caller) => this.caller = caller;

    public string Key         => "HotPay";
    public string Name        => "HotPay";
    /// <inheritdoc/>
    public string Description => "Operator płatności HotPay — BLIK, karty, przelewy.";
    public string Url         => "https://hotpay.pl";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "blik",     Name = "BLIK",            Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "card",     Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "transfer", Name = "Przelew online",  Description = "Przelew bankowy",  PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var paymentId = Guid.NewGuid().ToString("N");
        var (resultId, redirectUrl) = await caller.InitPaymentAsync(request, paymentId);

        if (resultId is null && redirectUrl is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "API error" };

        return new PaymentResponse
        {
            PaymentUniqueId = resultId,
            RedirectUrl     = redirectUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public Task<PaymentResponse> GetStatus(string paymentId)
        => Task.FromResult(new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = PaymentStatusEnum.Processing });

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
    {
        var hash        = request.Query.TryGetValue("HASH",         out var h) ? h.ToString() : string.Empty;
        var kwota       = request.Query.TryGetValue("KWOTA",        out var k) ? k.ToString() : string.Empty;
        var idPlatnosci = request.Query.TryGetValue("ID_PLATNOSCI", out var i) ? i.ToString() : string.Empty;
        var status      = request.Query.TryGetValue("STATUS",       out var s) ? s.ToString() : string.Empty;

        var payload = new TransactionStatusChangePayload
        {
            QueryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "HASH",         hash        },
                { "KWOTA",        kwota       },
                { "ID_PLATNOSCI", idPlatnosci },
                { "STATUS",       status      },
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
        var qs = payload.QueryParameters;
        var hash        = qs.TryGetValue("HASH",          out var h) ? h.ToString() : string.Empty;
        var kwota       = qs.TryGetValue("KWOTA",         out var k) ? k.ToString() : string.Empty;
        var idPlatnosci = qs.TryGetValue("ID_PLATNOSCI",  out var i) ? i.ToString() : string.Empty;
        var status      = qs.TryGetValue("STATUS",        out var s) ? s.ToString() : string.Empty;

        if (!caller.VerifyNotification(hash, kwota, idPlatnosci, status))
            return Task.FromResult(new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "Invalid hash" });

        var payStatus = status == "SUCCESS" ? PaymentStatusEnum.Finished : PaymentStatusEnum.Rejected;
        return Task.FromResult(new PaymentResponse { PaymentUniqueId = idPlatnosci, PaymentStatus = payStatus, ResponseObject = "OK" });
    }
}

/// <summary>Rozszerzenia DI dla HotPay.</summary>
public static class HotPayProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterHotPayProvider(this IServiceCollection services)
    {
        services.AddOptions<HotPayServiceOptions>();
        services.ConfigureOptions<HotPayConfigureOptions>();
        services.AddHttpClient("HotPay");
        services.AddTransient<IHotPayServiceCaller, HotPayServiceCaller>();
        services.AddTransient<HotPayProvider>();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<HotPayProvider>());
        services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<HotPayProvider>());
    }
}

/// <summary>Wczytuje opcje HotPay z konfiguracji.</summary>
public class HotPayConfigureOptions : IConfigureOptions<HotPayServiceOptions>
{
    private readonly IConfiguration configuration;
    /// <summary>Inicjalizuje instancję konfiguracji.</summary>
    public HotPayConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    /// <inheritdoc/>
    public void Configure(HotPayServiceOptions options)
    {
        var s = configuration.GetSection(HotPayServiceOptions.ConfigurationKey).Get<HotPayServiceOptions>();
        if (s is null) return;
        options.SecretHash = s.SecretHash;
        options.ServiceUrl = s.ServiceUrl;
        options.ReturnUrl  = s.ReturnUrl;
        options.NotifyUrl  = s.NotifyUrl;
    }
}
