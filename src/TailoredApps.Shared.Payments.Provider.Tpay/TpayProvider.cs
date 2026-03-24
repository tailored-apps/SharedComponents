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
    public static string ConfigurationKey => "Payments:Providers:Tpay";
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string MerchantId   { get; set; } = string.Empty;
    public string ApiUrl       { get; set; } = "https://api.tpay.com";
    public string ReturnUrl    { get; set; } = string.Empty;
    public string NotifyUrl    { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
}

file class TpayTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}

file class TpayTransactionRequest
{
    [JsonPropertyName("amount")]         public decimal Amount       { get; set; }
    [JsonPropertyName("description")]    public string  Description  { get; set; } = string.Empty;
    [JsonPropertyName("hiddenDescription")] public string? HiddenDescription { get; set; }
    [JsonPropertyName("lang")]           public string  Lang         { get; set; } = "pl";
    [JsonPropertyName("pay")]            public TpayPay Pay          { get; set; } = new();
    [JsonPropertyName("payer")]          public TpayPayer Payer      { get; set; } = new();
    [JsonPropertyName("callbacks")]      public TpayCallbacks Callbacks { get; set; } = new();
}

file class TpayPay
{
    [JsonPropertyName("groupId")]  public int?    GroupId  { get; set; }
    [JsonPropertyName("channel")] public string? Channel { get; set; }
}

file class TpayPayer
{
    [JsonPropertyName("email")]  public string Email { get; set; } = string.Empty;
    [JsonPropertyName("name")]   public string Name  { get; set; } = string.Empty;
}

file class TpayCallbacks
{
    [JsonPropertyName("payerUrls")] public TpayPayerUrls PayerUrls { get; set; } = new();
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
    [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
    [JsonPropertyName("transactionPaymentUrl")] public string? PaymentUrl { get; set; }
    [JsonPropertyName("title")]         public string? Title { get; set; }
}

file class TpayStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
}

public interface ITpayServiceCaller
{
    Task<string> GetAccessTokenAsync();
    Task<TpayTransactionResponse?> CreateTransactionAsync(string token, PaymentRequest request);
    Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId);
    bool VerifyNotification(string body, string signature);
}

public class TpayServiceCaller : ITpayServiceCaller
{
    private readonly TpayServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    public TpayServiceCaller(IOptions<TpayServiceOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        var content = new FormUrlEncodedContent([
            new("grant_type",    "client_credentials"),
            new("client_id",     options.ClientId),
            new("client_secret", options.ClientSecret),
        ]);
        var response = await client.PostAsync($"{options.ApiUrl}/oauth/auth", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TpayTokenResponse>(json)?.AccessToken ?? string.Empty;
    }

    public async Task<TpayTransactionResponse?> CreateTransactionAsync(string token, PaymentRequest request)
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

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ApiUrl}/transactions", content);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TpayTransactionResponse>(json);
    }

    public async Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync($"{options.ApiUrl}/transactions/{transactionId}");
        if (!response.IsSuccessStatusCode) return PaymentStatusEnum.Rejected;
        var json = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<TpayStatusResponse>(json)?.Status;
        return status switch
        {
            "correct"   => PaymentStatusEnum.Finished,
            "pending"   => PaymentStatusEnum.Processing,
            "error"     => PaymentStatusEnum.Rejected,
            "chargeback" => PaymentStatusEnum.Rejected,
            _           => PaymentStatusEnum.Created,
        };
    }

    public bool VerifyNotification(string body, string signature)
    {
        var input = body + options.SecurityCode;
        var hash  = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }
}

public class TpayProvider : IPaymentProvider
{
    private readonly ITpayServiceCaller caller;

    public TpayProvider(ITpayServiceCaller caller) => this.caller = caller;

    public string Key         => "Tpay";
    public string Name        => "Tpay";
    public string Description => "Operator płatności Tpay — przelewy, BLIK, karty.";
    public string Url         => "https://tpay.com";

    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
    {
        ICollection<PaymentChannel> channels =
        [
            new PaymentChannel { Id = "150", Name = "BLIK",            Description = "BLIK",             PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "103", Name = "Karta płatnicza", Description = "Visa, Mastercard", PaymentModel = PaymentModel.OneTime },
            new PaymentChannel { Id = "21",  Name = "Przelew online",  Description = "mTransfer, iPKO",  PaymentModel = PaymentModel.OneTime },
        ];
        return Task.FromResult(channels);
    }

    public async Task<PaymentResponse> RequestPayment(PaymentRequest request)
    {
        var token = await caller.GetAccessTokenAsync();
        var tx    = await caller.CreateTransactionAsync(token, request);
        return new PaymentResponse
        {
            PaymentUniqueId = tx?.TransactionId,
            RedirectUrl     = tx?.PaymentUrl,
            PaymentStatus   = PaymentStatusEnum.Created,
        };
    }

    public async Task<PaymentResponse> GetStatus(string paymentId)
    {
        var token  = await caller.GetAccessTokenAsync();
        var status = await caller.GetTransactionStatusAsync(token, paymentId);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

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
            if (doc.RootElement.TryGetProperty("tr_status", out var st))
                status = st.GetString() switch
                {
                    "TRUE"  => PaymentStatusEnum.Finished,
                    "FALSE" => PaymentStatusEnum.Rejected,
                    _       => PaymentStatusEnum.Processing,
                };
        }
        catch { /* ignore */ }

        return Task.FromResult(new PaymentResponse { PaymentStatus = status, ResponseObject = "OK" });
    }
}

public static class TpayProviderExtensions
{
    public static void RegisterTpayProvider(this IServiceCollection services)
    {
        services.AddOptions<TpayServiceOptions>();
        services.ConfigureOptions<TpayConfigureOptions>();
        services.AddHttpClient("Tpay");
        services.AddTransient<ITpayServiceCaller, TpayServiceCaller>();
    }
}

public class TpayConfigureOptions : IConfigureOptions<TpayServiceOptions>
{
    private readonly IConfiguration configuration;
    public TpayConfigureOptions(IConfiguration configuration) => this.configuration = configuration;
    public void Configure(TpayServiceOptions options)
    {
        var s = configuration.GetSection(TpayServiceOptions.ConfigurationKey).Get<TpayServiceOptions>();
        if (s is null) return;
        options.ClientId     = s.ClientId;
        options.ClientSecret = s.ClientSecret;
        options.MerchantId   = s.MerchantId;
        options.ApiUrl       = s.ApiUrl;
        options.ReturnUrl    = s.ReturnUrl;
        options.NotifyUrl    = s.NotifyUrl;
        options.SecurityCode = s.SecurityCode;
    }
}
