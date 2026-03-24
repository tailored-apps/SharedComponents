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

public class PayNowServiceOptions
{
    public static string ConfigurationKey => "Payments:Providers:PayNow";
    public string ApiKey       { get; set; } = string.Empty;
    public string SignatureKey { get; set; } = string.Empty;
    public string ApiUrl       { get; set; } = "https://api.paynow.pl";
    public string ReturnUrl    { get; set; } = string.Empty;
    public string ContinueUrl  { get; set; } = string.Empty;
}

file class PayNowPaymentRequest
{
    [JsonPropertyName("amount")]        public long   Amount      { get; set; }
    [JsonPropertyName("currency")]      public string Currency    { get; set; } = "PLN";
    [JsonPropertyName("externalId")]    public string ExternalId  { get; set; } = string.Empty;
    [JsonPropertyName("description")]   public string Description { get; set; } = string.Empty;
    [JsonPropertyName("buyer")]         public PayNowBuyer? Buyer { get; set; }
    [JsonPropertyName("continueUrl")]   public string? ContinueUrl { get; set; }
    [JsonPropertyName("returnUrl")]     public string? ReturnUrl { get; set; }
}

file class PayNowBuyer
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
}

file class PayNowPaymentResponse
{
    [JsonPropertyName("paymentId")]   public string? PaymentId  { get; set; }
    [JsonPropertyName("status")]      public string? Status     { get; set; }
    [JsonPropertyName("redirectUrl")] public string? RedirectUrl { get; set; }
}

file class PayNowStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
}

public interface IPayNowServiceCaller
{
    Task<PayNowPaymentResponse?> CreatePaymentAsync(PaymentRequest request);
    Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId);
    bool VerifySignature(string body, string signature);
}

public class PayNowServiceCaller : IPayNowServiceCaller
{
    private readonly PayNowServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

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

    public async Task<PayNowPaymentResponse?> CreatePaymentAsync(PaymentRequest request)
    {
        using var client = CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

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
        var response = await client.PostAsync($"{options.ApiUrl}/v2/payments", content);
        var json     = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PayNowPaymentResponse>(json);
    }

    public async Task<PaymentStatusEnum> GetPaymentStatusAsync(string paymentId)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{options.ApiUrl}/v2/payments/{paymentId}/status");
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

    public bool VerifySignature(string body, string signature)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(options.SignatureKey);
        var dataBytes = Encoding.UTF8.GetBytes(body);
        var computed  = Convert.ToBase64String(HMACSHA256.HashData(keyBytes, dataBytes));
        return string.Equals(computed, signature, StringComparison.Ordinal);
    }
}

public class PayNowProvider : IPaymentProvider
{
    private readonly IPayNowServiceCaller caller;

    public PayNowProvider(IPayNowServiceCaller caller) => this.caller = caller;

    public string Key         => "PayNow";
    public string Name        => "PayNow";
    public string Description => "Operator płatności PayNow (mBank) — BLIK, karty, przelewy.";
    public string Url         => "https://paynow.pl";

    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "BLIK",     Name = "BLIK",              Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "CARD",     Name = "Karta płatnicza",   Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "PBL",      Name = "Przelew bankowy",   Description = "Pay-by-link",      PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "TRANSFER", Name = "Przelew tradycyjny",Description = "Przelew bankowy",  PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var payment = await caller.CreatePaymentAsync(request);
        return new PaymentResponse
        {
            PaymentUniqueId = payment?.PaymentId,
            RedirectUrl     = payment?.RedirectUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var status = await caller.GetPaymentStatusAsync(paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

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

public static class PayNowProviderExtensions
{
    public static void RegisterPayNowProvider(this IServiceCollection services)
    {
        services.AddOptions<PayNowServiceOptions>();
        services.ConfigureOptions<PayNowConfigureOptions>();
        services.AddHttpClient("PayNow");
        services.AddTransient<IPayNowServiceCaller, PayNowServiceCaller>();
    }
}

public class PayNowConfigureOptions : IConfigureOptions<PayNowServiceOptions>
{
    private readonly IConfiguration configuration;
    public PayNowConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    public void Configure(PayNowServiceOptions options)
    {
        var s = configuration.GetSection(PayNowServiceOptions.ConfigurationKey).Get<PayNowServiceOptions>();
        if (s is null) return;
        options.ApiKey       = s.ApiKey;
        options.SignatureKey = s.SignatureKey;
        options.ApiUrl       = s.ApiUrl;
        options.ReturnUrl    = s.ReturnUrl;
        options.ContinueUrl  = s.ContinueUrl;
    }
}
