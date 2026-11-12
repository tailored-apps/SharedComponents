using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Payments.Security;

namespace TailoredApps.Shared.Payments.Provider.Tpay;

/// <summary>Configuration options for the Tpay payment provider. Bound from section <c>Payments:Providers:Tpay</c>.</summary>
public class TpayServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Tpay";
    /// <summary>ClientId.</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>ClientSecret.</summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>MerchantId.</summary>
    public string MerchantId { get; set; } = string.Empty;
    /// <summary>Base URL of the Tpay API endpoint.</summary>
    public string ServiceUrl { get; set; } = "https://api.tpay.com";

    /// <summary>Alias for <see cref="ServiceUrl"/> — kept for backwards compatibility.</summary>
    [Obsolete("Use ServiceUrl instead.")]
    public string ApiUrl
    {
        get => ServiceUrl;
        set => ServiceUrl = value;
    }
    /// <summary>ReturnUrl.</summary>
    public string ReturnUrl { get; set; } = string.Empty;
    /// <summary>NotifyUrl.</summary>
    public string NotifyUrl { get; set; } = string.Empty;
    /// <summary>
    /// The merchant's notification security code (verification code) from the Tpay panel. It is the
    /// secret of the <c>md5sum</c> field in every notification.
    /// </summary>
    public string SecurityCode { get; set; } = string.Empty;

    /// <summary>
    /// The origin Tpay serves its notification signing certificates from. The <c>x5u</c> address in the
    /// <c>X-JWS-Signature</c> header must point at this origin, and the root certificates are read from
    /// <c>/x509/tpay-jws-root.pem</c> below it. Production: <c>https://secure.tpay.com</c> (default);
    /// sandbox: <c>https://secure.sandbox.tpay.com</c>.
    /// </summary>
    public string JwsCertificateBaseUrl { get; set; } = "https://secure.tpay.com";

    /// <summary>
    /// Optional PEM text of the Tpay root certificates (the content of <c>tpay-jws-root.pem</c>: the issuing
    /// CA followed by the self-signed root). When set, trust is pinned to these certificates instead of the
    /// file downloaded from <see cref="JwsCertificateBaseUrl"/>.
    /// </summary>
    public string? JwsRootCertificatePem { get; set; }

    /// <summary>Reads every certificate of a PEM text without throwing on text that holds none.</summary>
    internal static bool TryReadCertificates(string? pem, [NotNullWhen(true)] out X509Certificate2Collection? certificates)
    {
        certificates = null;
        if (string.IsNullOrWhiteSpace(pem))
            return false;

        var collection = new X509Certificate2Collection();
        try
        {
            collection.ImportFromPem(pem);
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (collection.Count == 0)
            return false;

        certificates = collection;
        return true;
    }

    /// <summary>Whether <see cref="JwsCertificateBaseUrl"/> is an absolute https address.</summary>
    internal bool HasValidJwsCertificateBaseUrl
        => Uri.TryCreate(JwsCertificateBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
}

file class TpayTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
}

file class TpayTransactionRequest
{
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("hiddenDescription")] public string? HiddenDescription { get; set; }
    [JsonPropertyName("lang")] public string Lang { get; set; } = "pl";
    [JsonPropertyName("pay")] public TpayPay Pay { get; set; } = new();
    [JsonPropertyName("payer")] public TpayPayer Payer { get; set; } = new();
    [JsonPropertyName("callbacks")] public TpayCallbacks Callbacks { get; set; } = new();
}

file class TpayPay
{
    [JsonPropertyName("groupId")] public int? GroupId { get; set; }
    [JsonPropertyName("channel")] public string? Channel { get; set; }
}

file class TpayPayer
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    /// <inheritdoc/>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

file class TpayCallbacks
{
    [JsonPropertyName("payerUrls")] public TpayPayerUrls PayerUrls { get; set; } = new();
    [JsonPropertyName("notification")] public TpayNotification Notification { get; set; } = new();
}

file class TpayPayerUrls
{
    [JsonPropertyName("success")] public string Success { get; set; } = string.Empty;
    [JsonPropertyName("error")] public string Error { get; set; } = string.Empty;
}

file class TpayNotification
{
    /// <inheritdoc/>
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
}

file class TpayTransactionResponse
{
    [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
    [JsonPropertyName("transactionPaymentUrl")] public string? PaymentUrl { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
}

file class TpayStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
}

/// <summary>Abstrakcja nad Tpay REST API.</summary>
public interface ITpayServiceCaller
{
    /// <summary>Wywołanie API.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    /// <summary>Wywołanie API.</summary>
    Task<(string? transactionId, string? paymentUrl)> CreateTransactionAsync(string token, PaymentRequest request, CancellationToken cancellationToken = default);
    /// <summary>Wywołanie API.</summary>
    Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Verifies a transaction notification: the <c>md5sum</c> field (proves the notification is addressed
    /// to this merchant) and the <c>X-JWS-Signature</c> header (proves Tpay sent exactly this body).
    /// </summary>
    /// <param name="body">The raw <c>application/x-www-form-urlencoded</c> request body, exactly as received.</param>
    /// <param name="jwsSignature">The value of the <c>X-JWS-Signature</c> header.</param>
    /// <param name="cancellationToken">Cancels the download of the signing certificates.</param>
    /// <returns><c>true</c> only when both checks pass.</returns>
    Task<bool> VerifyNotificationAsync(string body, string jwsSignature, CancellationToken cancellationToken = default);
}

/// <summary>Implementacja <see cref="ITpayServiceCaller"/>.</summary>
public class TpayServiceCaller : ITpayServiceCaller
{
    /// <summary>How long a downloaded signing or root certificate is reused before it is fetched again.</summary>
    private static readonly TimeSpan CertificateLifetime = TimeSpan.FromHours(1);

    private const string RootCertificatePath = "/x509/tpay-jws-root.pem";
    private const string PinnedRootKey = "pinned-root";

    private readonly TpayServiceOptions options;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, CachedCertificates> certificates = new(StringComparer.Ordinal);

    /// <summary>Inicjalizuje instancję callera.</summary>
    /// <param name="options">The provider options.</param>
    /// <param name="httpClientFactory">Creates the <c>Tpay</c> HTTP client.</param>
    /// <param name="timeProvider">The clock used to expire cached certificates; the system clock when omitted.</param>
    public TpayServiceCaller(IOptions<TpayServiceOptions> options, IHttpClientFactory httpClientFactory, TimeProvider? timeProvider = null)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    private sealed record CachedCertificates(X509Certificate2Collection Certificates, DateTimeOffset ExpiresAt);

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        var content = new FormUrlEncodedContent([
            new("grant_type",    "client_credentials"),
            new("client_id",     options.ClientId),
            new("client_secret", options.ClientSecret),
        ]);
        var response = await client.PostAsync($"{options.ServiceUrl}/oauth/auth", content, cancellationToken);
        await PaymentGatewayException.ThrowIfNotSuccessAsync("Tpay", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TpayTokenResponse>(json)?.AccessToken ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<(string? transactionId, string? paymentUrl)> CreateTransactionAsync(string token, PaymentRequest request, CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new TpayTransactionRequest
        {
            Amount = request.Amount,
            Description = request.Title ?? request.Description ?? "Order",
            // Returned as tr_crc in the notification, which is how the host recognises its own order.
            HiddenDescription = string.IsNullOrWhiteSpace(request.AdditionalData) ? null : request.AdditionalData,
            Payer = new TpayPayer { Email = request.Email ?? string.Empty, Name = $"{request.FirstName} {request.Surname}".Trim() },
            Callbacks = new TpayCallbacks
            {
                PayerUrls = new TpayPayerUrls { Success = options.ReturnUrl, Error = options.ReturnUrl },
                Notification = new TpayNotification { Url = options.NotifyUrl, Email = request.Email ?? string.Empty },
            },
        };

        if (!string.IsNullOrWhiteSpace(request.PaymentChannel))
            body.Pay = new TpayPay { Channel = request.PaymentChannel };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{options.ServiceUrl}/transactions", content, cancellationToken);
        // 5xx, 408 and 429 mean the gateway did not handle the call; a 4xx answer is a refusal reported below.
        await PaymentGatewayException.ThrowIfUnavailableAsync("Tpay", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tx = JsonSerializer.Deserialize<TpayTransactionResponse>(json);
        return (tx?.TransactionId, tx?.PaymentUrl);
    }

    /// <inheritdoc/>
    public async Task<PaymentStatusEnum> GetTransactionStatusAsync(string token, string transactionId, CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient("Tpay");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var safeTransactionId = PaymentIdentifier.EnsureSafe(transactionId, nameof(transactionId));
        var response = await client.GetAsync($"{options.ServiceUrl}/transactions/{safeTransactionId}", cancellationToken);
        // A failed status query says nothing about the payment: never report it as rejected.
        await PaymentGatewayException.ThrowIfNotSuccessAsync("Tpay", response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = JsonSerializer.Deserialize<TpayStatusResponse>(json)?.Status;
        return status switch
        {
            "correct" => PaymentStatusEnum.Finished,
            "pending" => PaymentStatusEnum.Processing,
            "error" => PaymentStatusEnum.Rejected,
            "chargeback" => PaymentStatusEnum.Rejected,
            _ => PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyNotificationAsync(string body, string jwsSignature, CancellationToken cancellationToken = default)
    {
        // Fail closed: without the security code the md5sum is computable by anyone.
        if (!WebhookSignature.IsSecretConfigured(options.SecurityCode) || string.IsNullOrEmpty(body) || string.IsNullOrEmpty(jwsSignature))
            return false;

        // The checksum needs the merchant's secret, so it goes first: a request that fails it never
        // makes this service download a certificate.
        if (!VerifyChecksum(HttpUtility.ParseQueryString(body)))
            return false;

        if (!TryReadJws(body, jwsSignature, out var certificateUrl, out var signedData, out var signature))
            return false;

        var (verified, usedCachedCertificate) = await VerifyJwsAsync(certificateUrl, signedData, signature, allowCached: true, cancellationToken);
        if (!verified && usedCachedCertificate)
        {
            // Tpay may have replaced its certificate since it was cached: check once more against a fresh copy.
            (verified, _) = await VerifyJwsAsync(certificateUrl, signedData, signature, allowCached: false, cancellationToken);
        }

        return verified;
    }

    /// <summary>
    /// Checks <c>md5sum = md5(id + tr_id + tr_amount + tr_crc + security code)</c>. The amount is taken as
    /// received and, failing that, with two decimal places, which is how Tpay's own SDK formats it.
    /// </summary>
    private bool VerifyChecksum(NameValueCollection form)
    {
        var received = form["md5sum"];
        if (string.IsNullOrEmpty(received))
            return false;

        var prefix = (form["id"] ?? string.Empty) + (form["tr_id"] ?? string.Empty);
        var amount = form["tr_amount"] ?? string.Empty;
        var suffix = (form["tr_crc"] ?? string.Empty) + options.SecurityCode;

        if (WebhookSignature.FixedTimeEqualsIgnoreCase(Md5Hex(prefix + amount + suffix), received))
            return true;

        return decimal.TryParse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            && WebhookSignature.FixedTimeEqualsIgnoreCase(Md5Hex(prefix + value.ToString("0.00", CultureInfo.InvariantCulture) + suffix), received);
    }

    private static string Md5Hex(string text)
        => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// Splits the compact JWS with a detached payload, checks its header and rebuilds the signed text from
    /// the request body.
    /// </summary>
    private bool TryReadJws(string body, string jws, out Uri certificateUrl, out byte[] signedData, out byte[] signature)
    {
        certificateUrl = null!;
        signedData = [];
        signature = [];

        var segments = jws.Split('.');
        if (segments.Length != 3 || segments[0].Length == 0 || segments[2].Length == 0)
            return false;

        string? x5u;
        try
        {
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(segments[0]));
            var root = header.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            // Tpay signs with RSA PKCS#1 v1.5 and SHA-256. Any other declared algorithm ("none" included) is refused.
            if (root.TryGetProperty("alg", out var algorithm)
                && !(algorithm.ValueKind == JsonValueKind.String && algorithm.ValueEquals("RS256")))
                return false;

            x5u = root.TryGetProperty("x5u", out var url) && url.ValueKind == JsonValueKind.String ? url.GetString() : null;
            signature = Base64Url.DecodeFromChars(segments[2]);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }

        if (!TryGetTrustedCertificateUrl(x5u, out certificateUrl))
            return false;

        // The payload segment of the JWS is detached: it is the request body, base64url-encoded without padding.
        signedData = Encoding.ASCII.GetBytes(segments[0] + "." + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(body)));
        return true;
    }

    /// <summary>
    /// Accepts a certificate address only when it is an https address on the configured Tpay origin. The
    /// whole origin is compared, not a text prefix, so <c>https://secure.tpay.com.example.net</c> is refused.
    /// </summary>
    private bool TryGetTrustedCertificateUrl(string? x5u, out Uri certificateUrl)
    {
        certificateUrl = null!;
        if (!Uri.TryCreate(options.JwsCertificateBaseUrl, UriKind.Absolute, out var trusted)
            || !Uri.TryCreate(x5u, UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || Uri.Compare(candidate, trusted, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
            return false;

        certificateUrl = candidate;
        return true;
    }

    private async Task<(bool Verified, bool UsedCachedCertificate)> VerifyJwsAsync(Uri certificateUrl, byte[] signedData, byte[] signature, bool allowCached, CancellationToken cancellationToken)
    {
        var (signing, signingFromCache) = await GetCertificatesAsync(certificateUrl, allowCached, cancellationToken);
        var (trusted, trustedFromCache) = await GetTrustedCertificatesAsync(allowCached, cancellationToken);
        var usedCache = signingFromCache || trustedFromCache;

        // The signing certificate is the first one of the file x5u points at.
        if (signing is null || trusted is null || !IsIssuedByTrustedRoot(signing[0], trusted))
            return (false, usedCache);

        using var publicKey = signing[0].GetRSAPublicKey();
        var verified = publicKey is not null
            && publicKey.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (verified, usedCache);
    }

    private Task<(X509Certificate2Collection? Certificates, bool FromCache)> GetTrustedCertificatesAsync(bool allowCached, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.JwsRootCertificatePem))
            return Task.FromResult<(X509Certificate2Collection?, bool)>((GetPinnedCertificates(options.JwsRootCertificatePem), false));

        var rootUrl = new Uri(new Uri(options.JwsCertificateBaseUrl), RootCertificatePath);
        return GetCertificatesAsync(rootUrl, allowCached, cancellationToken);
    }

    /// <summary>Parses the pinned root once and keeps it for the lifetime of the caller; unreadable PEM text verifies nothing.</summary>
    private X509Certificate2Collection? GetPinnedCertificates(string pem)
    {
        if (certificates.TryGetValue(PinnedRootKey, out var cached))
            return cached.Certificates;

        if (!TpayServiceOptions.TryReadCertificates(pem, out var pinned))
            return null;

        certificates[PinnedRootKey] = new CachedCertificates(pinned, DateTimeOffset.MaxValue);
        return pinned;
    }

    private async Task<(X509Certificate2Collection? Certificates, bool FromCache)> GetCertificatesAsync(Uri url, bool allowCached, CancellationToken cancellationToken)
    {
        var key = url.AbsoluteUri;
        var now = timeProvider.GetUtcNow();
        if (allowCached && certificates.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
            return (cached.Certificates, true);

        using var client = httpClientFactory.CreateClient("Tpay");
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (null, false);

        var pem = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!TpayServiceOptions.TryReadCertificates(pem, out var downloaded))
            return (null, false);

        certificates[key] = new CachedCertificates(downloaded, now + CertificateLifetime);
        return (downloaded, false);
    }

    /// <summary>
    /// Builds the chain of <paramref name="certificate"/> up to a self-signed certificate of
    /// <paramref name="trusted"/>, checking signatures and validity dates on the way. The certificate
    /// store of the machine is not consulted and nothing is downloaded.
    /// </summary>
    /// <remarks>
    /// Tpay publishes its root as a bundle: the CA that issues the signing certificate, followed by the
    /// self-signed root. Only the self-signed ones are trust anchors; the others may complete the chain.
    /// </remarks>
    private static bool IsIssuedByTrustedRoot(X509Certificate2 certificate, X509Certificate2Collection trusted)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;

        foreach (var candidate in trusted)
        {
            var selfSigned = candidate.SubjectName.RawData.AsSpan().SequenceEqual(candidate.IssuerName.RawData);
            (selfSigned ? chain.ChainPolicy.CustomTrustStore : chain.ChainPolicy.ExtraStore).Add(candidate);
        }

        return chain.Build(certificate);
    }
}

/// <summary>Implementacja <see cref="IPaymentProvider"/> dla Tpay.</summary>
public class TpayProvider : IPaymentProvider, IWebhookPaymentProvider
{
    private readonly ITpayServiceCaller caller;

    /// <summary>Inicjalizuje instancję providera.</summary>
    public TpayProvider(ITpayServiceCaller caller) => this.caller = caller;

    /// <inheritdoc/>
    public string Key => "Tpay";
    /// <inheritdoc/>
    public string Name => "Tpay";
    /// <inheritdoc/>
    public string Description => "Operator płatności Tpay — przelewy, BLIK, karty.";
    /// <inheritdoc/>
    public string Url => "https://tpay.com";

    /// <inheritdoc/>
    public Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency, CancellationToken cancellationToken = default)
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
    public async Task<PaymentResponse> RequestPayment(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var token = await caller.GetAccessTokenAsync(cancellationToken);
        var (transactionId, paymentUrl) = await caller.CreateTransactionAsync(token, request, cancellationToken);
        if (transactionId is null)
            return new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = "API error" };
        return new PaymentResponse
        {
            PaymentUniqueId = transactionId,
            RedirectUrl = paymentUrl,
            PaymentStatus = PaymentStatusEnum.Created,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetStatus(string paymentId, CancellationToken cancellationToken = default)
    {
        var token = await caller.GetAccessTokenAsync(cancellationToken);
        var status = await caller.GetTransactionStatusAsync(token, paymentId, cancellationToken);
        return new PaymentResponse { PaymentUniqueId = paymentId, PaymentStatus = status };
    }

    // ─── IWebhookPaymentProvider ─────────────────────────────────────────────

    /// <summary>The header that carries the JWS signature of a notification.</summary>
    public const string SignatureHeader = "X-JWS-Signature";

    /// <summary>
    /// The body Tpay expects in the answer to a notification it should not send again. Returned in
    /// <see cref="PaymentResponse.ResponseObject"/>; answer with HTTP 200 and exactly this text.
    /// </summary>
    public const string Acknowledgement = "TRUE";

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="PaymentWebhookRequest.Body"/> must be the raw <c>application/x-www-form-urlencoded</c>
    /// body: the signature covers its exact bytes, so a re-encoded form does not verify.
    /// </remarks>
    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var signature = request.Headers.TryGetValue(SignatureHeader, out var s) ? s.ToString() : string.Empty;
        return ProcessNotificationAsync(request.Body, signature, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload, CancellationToken cancellationToken = default)
    {
        var signature = payload.QueryParameters.TryGetValue(SignatureHeader, out var s) ? s.ToString() : string.Empty;
        var result = await ProcessNotificationAsync(payload.Payload?.ToString(), signature, cancellationToken);
        return result.ToPaymentResponse();
    }

    private async Task<PaymentWebhookResult> ProcessNotificationAsync(string? body, string signature, CancellationToken cancellationToken)
    {
        body ??= string.Empty;
        if (!await caller.VerifyNotificationAsync(body, signature, cancellationToken))
            return PaymentWebhookResult.Reject(PaymentWebhookRejection.InvalidSignature, "Invalid signature");

        var form = HttpUtility.ParseQueryString(body);
        var status = form["tr_status"]?.ToUpperInvariant() switch
        {
            "TRUE" or "PAID" => PaymentStatusEnum.Finished,
            "CHARGEBACK" or "FALSE" => PaymentStatusEnum.Rejected,
            _ => PaymentStatusEnum.Processing,
        };

        // tr_crc is the merchant's own reference (hiddenDescription of the transaction); tr_id is Tpay's
        // transaction title. The transaction id returned when the payment was created is not in the notification.
        var reference = form["tr_crc"];
        var paymentUniqueId = string.IsNullOrEmpty(reference) ? form["tr_id"] : reference;

        return PaymentWebhookResult.FromStatus(new PaymentResponse { PaymentUniqueId = paymentUniqueId, PaymentStatus = status, ResponseObject = Acknowledgement });
    }
}

/// <summary>Rozszerzenia DI dla Tpay.</summary>
public static class TpayProviderExtensions
{
    /// <summary>Rejestruje provider i jego zależności w kontenerze DI.</summary>
    public static void RegisterTpayProvider(this IServiceCollection services)
    {
        // Fail when the host starts, not on the first payment: these are the settings the provider cannot work without.
        services.AddOptions<TpayServiceOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), $"'{TpayServiceOptions.ConfigurationKey}:ClientId' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), $"'{TpayServiceOptions.ConfigurationKey}:ClientSecret' is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecurityCode), $"'{TpayServiceOptions.ConfigurationKey}:SecurityCode' is not configured.")
            .Validate(o => o.HasValidJwsCertificateBaseUrl, $"'{TpayServiceOptions.ConfigurationKey}:JwsCertificateBaseUrl' must be an absolute https address.")
            .Validate(o => string.IsNullOrWhiteSpace(o.JwsRootCertificatePem) || TpayServiceOptions.TryReadCertificates(o.JwsRootCertificatePem, out _), $"'{TpayServiceOptions.ConfigurationKey}:JwsRootCertificatePem' is not a PEM certificate.")
            .ValidateOnStart();
        services.ConfigureOptions<TpayConfigureOptions>();
        services.AddHttpClient("Tpay");
        // Singleton: the caller keeps the downloaded signing certificates between notifications.
        services.AddSingleton<ITpayServiceCaller, TpayServiceCaller>();
        services.AddTransient<TpayProvider>();
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
        options.ClientId = s.ClientId;
        options.ClientSecret = s.ClientSecret;
        options.MerchantId = s.MerchantId;
        options.ServiceUrl = s.ServiceUrl;
        options.ReturnUrl = s.ReturnUrl;
        options.NotifyUrl = s.NotifyUrl;
        options.SecurityCode = s.SecurityCode;
        options.JwsCertificateBaseUrl = s.JwsCertificateBaseUrl;
        options.JwsRootCertificatePem = s.JwsRootCertificatePem;
    }
}
