using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// A certificate chain shaped like the one Tpay publishes: a self-signed root, the CA that issues the
/// signing certificate, and the signing certificate itself.
/// </summary>
public sealed class TpayCertificateChain : IDisposable
{
    private readonly RSA rootKey = RSA.Create(2048);
    private readonly RSA issuerKey = RSA.Create(2048);
    private readonly X509Certificate2 issuerWithKey;

    public TpayCertificateChain()
    {
        var now = DateTimeOffset.UtcNow;

        var rootRequest = new CertificateRequest("CN=Test Tpay Root CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        Root = rootRequest.CreateSelfSigned(now.AddDays(-2), now.AddYears(2));

        var issuerRequest = new CertificateRequest("CN=Test Tpay Issuing CA", issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        issuerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        issuerRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(issuerRequest.PublicKey, false));
        issuerRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Root, true, false));
        Issuer = issuerRequest.Create(Root, now.AddDays(-2), now.AddYears(1), [1, 2, 3, 4]);
        issuerWithKey = Issuer.CopyWithPrivateKey(issuerKey);

        (Signing, SigningKey) = IssueSigningCertificate(now.AddDays(-1), now.AddDays(30));
    }

    public X509Certificate2 Root { get; }

    public X509Certificate2 Issuer { get; }

    public X509Certificate2 Signing { get; }

    public RSA SigningKey { get; }

    /// <summary>The content of <c>tpay-jws-root.pem</c>: the issuing CA followed by the root.</summary>
    public string RootBundlePem => Issuer.ExportCertificatePem() + "\n" + Root.ExportCertificatePem() + "\n";

    public (X509Certificate2 Certificate, RSA Key) IssueSigningCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=notification.tpay.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Issuer, true, false));
        var serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        serial[0] &= 0x7F;
        return (request.Create(issuerWithKey, notBefore, notAfter, serial), key);
    }

    public void Dispose()
    {
        Root.Dispose();
        Issuer.Dispose();
        issuerWithKey.Dispose();
        Signing.Dispose();
        SigningKey.Dispose();
        rootKey.Dispose();
        issuerKey.Dispose();
    }
}

/// <summary>Verification of Tpay transaction notifications: <c>md5sum</c> and the <c>X-JWS-Signature</c> header.</summary>
public sealed class TpayNotificationVerificationTests : IClassFixture<TpayCertificateChain>
{
    private const string Origin = "https://secure.tpay.test";
    private const string SigningUrl = Origin + "/x509/notifications-jws.pem";
    private const string RootUrl = Origin + "/x509/tpay-jws-root.pem";
    private const string SecurityCode = "s3curity-c0de";

    private readonly TpayCertificateChain chain;

    public TpayNotificationVerificationTests(TpayCertificateChain chain) => this.chain = chain;

    private sealed class CertificateServer : HttpMessageHandler
    {
        public ConcurrentDictionary<string, string> Files { get; } = new();

        public ConcurrentQueue<string> Requests { get; } = new();

        public int Count(string url) => Requests.Count(r => r == url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);

            var url = request.RequestUri!.AbsoluteUri;
            Requests.Enqueue(url);
            return Task.FromResult(Files.TryGetValue(url, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private CertificateServer Server()
    {
        var server = new CertificateServer();
        server.Files[SigningUrl] = chain.Signing.ExportCertificatePem();
        server.Files[RootUrl] = chain.RootBundlePem;
        return server;
    }

    private static TpayServiceCaller Caller(CertificateServer server, Action<TpayServiceOptions> configure = null, TimeProvider timeProvider = null)
    {
        var options = new TpayServiceOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            SecurityCode = SecurityCode,
            JwsCertificateBaseUrl = Origin,
        };
        configure?.Invoke(options);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(server, disposeHandler: false));
        return new TpayServiceCaller(Options.Create(options), factory.Object, timeProvider);
    }

    private static string Md5(string text) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>A notification body as Tpay posts it, with the checksum of the given security code.</summary>
    private static string Body(string status = "TRUE", string amount = "10.00", string securityCode = SecurityCode, string checksumAmount = null)
    {
        const string id = "1010";
        const string transaction = "TR-ABC-123XYZ";
        const string reference = "order-7";
        var checksum = Md5(id + transaction + (checksumAmount ?? amount) + reference + securityCode);
        return $"id={id}&tr_id={transaction}&tr_date=2026-11-12+10%3A00%3A00&tr_crc={reference}&tr_amount={amount}&tr_paid={amount}"
             + $"&tr_desc=Order+7&tr_status={status}&tr_error=none&tr_email=buyer%40example.com&md5sum={checksum}";
    }

    /// <summary>A compact JWS with a detached payload, as in the <c>X-JWS-Signature</c> header.</summary>
    private static string Sign(RSA key, string body, string x5u = SigningUrl, string algorithm = "RS256")
    {
        var headerJson = algorithm is null
            ? JsonSerializer.Serialize(new { x5u })
            : JsonSerializer.Serialize(new { alg = algorithm, x5u });
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(body));
        var signature = key.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + ".." + Base64Url.EncodeToString(signature);
    }

    [Fact]
    public async Task When_Notification_Is_Signed_By_Tpay_Should_Verify()
    {
        // arrange
        var server = Server();
        var body = Body();

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body));

        // assert
        Assert.True(verified);
        Assert.Equal(1, server.Count(SigningUrl));
        Assert.Equal(1, server.Count(RootUrl));
    }

    [Fact]
    public async Task When_Header_Declares_No_Algorithm_Should_Verify_As_RS256()
    {
        var body = Body();

        Assert.True(await Caller(Server()).VerifyNotificationAsync(body, Sign(chain.SigningKey, body, algorithm: null)));
    }

    [Fact]
    public async Task When_Root_Is_Pinned_Should_Verify_Without_Downloading_It()
    {
        // arrange
        var server = Server();
        server.Files.TryRemove(RootUrl, out _);
        var caller = Caller(server, o => o.JwsRootCertificatePem = chain.RootBundlePem);
        var body = Body();

        // act
        var first = await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body));
        var second = await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body));

        // assert
        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, server.Count(RootUrl));
    }

    [Fact]
    public async Task When_Pinned_Root_Is_Not_A_Certificate_Should_Reject()
    {
        var body = Body();
        var caller = Caller(Server(), o => o.JwsRootCertificatePem = "not a certificate");

        Assert.False(await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Body_Is_Changed_After_Signing_Should_Reject()
    {
        // arrange: tr_status is not part of md5sum, so only the JWS can catch this change
        var signed = Body(status: "FALSE");
        var tampered = Body(status: "TRUE");

        // act
        var verified = await Caller(Server()).VerifyNotificationAsync(tampered, Sign(chain.SigningKey, signed));

        // assert
        Assert.False(verified);
    }

    [Fact]
    public async Task When_Checksum_Was_Made_With_Another_Security_Code_Should_Reject_Without_Downloading_Certificates()
    {
        // arrange: a genuine Tpay notification addressed to another merchant
        var server = Server();
        var body = Body(securityCode: "another-merchant");

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body));

        // assert
        Assert.False(verified);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task When_Checksum_Is_Missing_Should_Reject()
    {
        const string body = "id=1010&tr_id=TR-1&tr_amount=10.00&tr_crc=order-7&tr_status=TRUE";

        Assert.False(await Caller(Server()).VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Checksum_Was_Made_From_Amount_With_Two_Decimals_Should_Verify()
    {
        // Tpay's SDK formats tr_amount to two decimal places before hashing it.
        var body = Body(amount: "10", checksumAmount: "10.00");

        Assert.True(await Caller(Server()).VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Checksum_Is_Upper_Case_Should_Verify()
    {
        var body = Body();
        var checksum = body[(body.IndexOf("md5sum=", StringComparison.Ordinal) + 7)..];
        var upper = body.Replace(checksum, checksum.ToUpperInvariant(), StringComparison.Ordinal);

        Assert.True(await Caller(Server()).VerifyNotificationAsync(upper, Sign(chain.SigningKey, upper)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task When_Security_Code_Is_Not_Configured_Should_Reject(string securityCode)
    {
        var body = Body(securityCode: securityCode);
        var caller = Caller(Server(), o => o.SecurityCode = securityCode);

        Assert.False(await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Theory]
    [InlineData("", "sig")]
    [InlineData("id=1", "")]
    [InlineData(null, "sig")]
    [InlineData("id=1", null)]
    public async Task When_Body_Or_Signature_Is_Missing_Should_Reject(string body, string signature)
    {
        Assert.False(await Caller(Server()).VerifyNotificationAsync(body, signature));
    }

    [Theory]
    [InlineData("https://secure.tpay.test.evil.example/x509/notifications-jws.pem")]
    [InlineData("https://evil.example/x509/notifications-jws.pem")]
    [InlineData("http://secure.tpay.test/x509/notifications-jws.pem")]
    [InlineData("https://secure.tpay.test:8443/x509/notifications-jws.pem")]
    [InlineData("https://secure.tpay.test@evil.example/x509/notifications-jws.pem")]
    [InlineData("/x509/notifications-jws.pem")]
    [InlineData("")]
    public async Task When_Certificate_Address_Is_Outside_The_Tpay_Origin_Should_Reject_Without_Downloading_It(string x5u)
    {
        // arrange: the attacker serves a certificate of their own under the forged address
        var server = Server();
        server.Files[x5u] = chain.Signing.ExportCertificatePem();
        var body = Body();

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body, x5u));

        // assert
        Assert.False(verified);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task When_Configured_Origin_Is_Not_An_Address_Should_Reject()
    {
        var body = Body();
        var caller = Caller(Server(), o => o.JwsCertificateBaseUrl = "secure.tpay.test");

        Assert.False(await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Signing_Certificate_Is_Not_Issued_By_The_Trusted_Root_Should_Reject()
    {
        // arrange: a certificate chain of the attacker's own, served from the trusted origin's path
        using var foreign = new TpayCertificateChain();
        var server = Server();
        server.Files[SigningUrl] = foreign.Signing.ExportCertificatePem();
        var body = Body();

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(foreign.SigningKey, body));

        // assert
        Assert.False(verified);
    }

    [Fact]
    public async Task When_Root_Bundle_Has_No_Self_Signed_Root_Should_Reject()
    {
        // arrange: the issuing CA alone is not a trust anchor
        var server = Server();
        server.Files[RootUrl] = chain.Issuer.ExportCertificatePem();
        var body = Body();

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body));

        // assert
        Assert.False(verified);
    }

    [Fact]
    public async Task When_Signing_Certificate_Has_Expired_Should_Reject()
    {
        // arrange
        var (expired, key) = chain.IssueSigningCertificate(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
        using var _ = expired;
        using var __ = key;
        var server = Server();
        server.Files[SigningUrl] = expired.ExportCertificatePem();
        var body = Body();

        // act
        var verified = await Caller(server).VerifyNotificationAsync(body, Sign(key, body));

        // assert
        Assert.False(verified);
    }

    [Fact]
    public async Task When_Signature_Was_Made_With_Another_Key_Should_Reject()
    {
        using var other = RSA.Create(2048);
        var body = Body();

        Assert.False(await Caller(Server()).VerifyNotificationAsync(body, Sign(other, body)));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS256")]
    [InlineData("RS512")]
    public async Task When_Header_Declares_Another_Algorithm_Should_Reject(string algorithm)
    {
        var body = Body();

        Assert.False(await Caller(Server()).VerifyNotificationAsync(body, Sign(chain.SigningKey, body, algorithm: algorithm)));
    }

    [Theory]
    [InlineData("only-one-segment")]
    [InlineData("two.segments")]
    [InlineData("a.b.c.d")]
    [InlineData("..signature")]
    [InlineData("header..")]
    [InlineData("!!!..c2ln")]                 // header is not base64url
    [InlineData("bm90LWpzb24..c2ln")]         // header is "not-json"
    [InlineData("WzFd..c2ln")]                // header is "[1]"
    [InlineData("eyJhbGciOiJSUzI1NiJ9..c2ln")] // header has no x5u
    public async Task When_Signature_Header_Is_Malformed_Should_Reject(string signature)
    {
        var server = Server();

        Assert.False(await Caller(server).VerifyNotificationAsync(Body(), signature));
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task When_Signature_Segment_Is_Not_Base64Url_Should_Reject()
    {
        var body = Body();
        var header = Sign(chain.SigningKey, body).Split('.')[0];

        Assert.False(await Caller(Server()).VerifyNotificationAsync(body, header + "..***"));
    }

    [Theory]
    [InlineData(SigningUrl)]
    [InlineData(RootUrl)]
    public async Task When_A_Certificate_Cannot_Be_Downloaded_Should_Reject(string missing)
    {
        var server = Server();
        server.Files.TryRemove(missing, out _);
        var body = Body();

        Assert.False(await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Downloaded_File_Holds_No_Certificate_Should_Reject()
    {
        var server = Server();
        server.Files[SigningUrl] = "<html>maintenance</html>";
        var body = Body();

        Assert.False(await Caller(server).VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
    }

    [Fact]
    public async Task When_Certificates_Were_Downloaded_Should_Reuse_Them_For_An_Hour()
    {
        // arrange
        var server = Server();
        var clock = new ManualTimeProvider();
        var caller = Caller(server, timeProvider: clock);
        var body = Body();
        var signature = Sign(chain.SigningKey, body);

        // act
        Assert.True(await caller.VerifyNotificationAsync(body, signature));
        clock.Now += TimeSpan.FromMinutes(59);
        Assert.True(await caller.VerifyNotificationAsync(body, signature));
        var downloadsWithinTheHour = server.Requests.Count;
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.True(await caller.VerifyNotificationAsync(body, signature));

        // assert
        Assert.Equal(2, downloadsWithinTheHour);
        Assert.Equal(4, server.Requests.Count);
    }

    [Fact]
    public async Task When_Tpay_Replaced_Its_Certificate_Should_Fetch_It_Again_And_Verify()
    {
        // arrange: the first notification caches the certificate, then Tpay starts signing with a new one
        var server = Server();
        var caller = Caller(server);
        var body = Body();
        Assert.True(await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));

        var (replacement, key) = chain.IssueSigningCertificate(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        using var _ = replacement;
        using var __ = key;
        server.Files[SigningUrl] = replacement.ExportCertificatePem();

        // act
        var verified = await caller.VerifyNotificationAsync(body, Sign(key, body));

        // assert
        Assert.True(verified);
        Assert.Equal(2, server.Count(SigningUrl));
    }

    [Fact]
    public async Task When_Forged_Signature_Fails_Against_Cached_Certificates_Should_Check_Fresh_Ones_Once()
    {
        // arrange
        var server = Server();
        var caller = Caller(server);
        var body = Body();
        Assert.True(await caller.VerifyNotificationAsync(body, Sign(chain.SigningKey, body)));
        using var other = RSA.Create(2048);

        // act
        var verified = await caller.VerifyNotificationAsync(body, Sign(other, body));

        // assert
        Assert.False(verified);
        Assert.Equal(2, server.Count(SigningUrl));
        Assert.Equal(2, server.Count(RootUrl));
    }

    [Fact]
    public async Task When_Download_Is_Cancelled_Should_Propagate_The_Cancellation()
    {
        var body = Body();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Caller(Server()).VerifyNotificationAsync(body, Sign(chain.SigningKey, body), cancellation.Token));
    }

    [Fact]
    public async Task When_Provider_Uses_The_Real_Caller_Should_Accept_A_Signed_Notification_End_To_End()
    {
        // arrange
        var provider = new TpayProvider(Caller(Server()));
        var body = Body(status: "TRUE");

        // act
        var result = await provider.HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            ContentType = "application/x-www-form-urlencoded",
            Headers = new Dictionary<string, StringValues> { { "x-jws-signature", Sign(chain.SigningKey, body) } },
        });

        // assert
        var processed = Assert.IsType<PaymentWebhookProcessed>(result);
        Assert.Equal(PaymentStatusEnum.Finished, processed.Payment.PaymentStatus);
        Assert.Equal("order-7", processed.Payment.PaymentUniqueId);
        Assert.Equal("TRUE", processed.Payment.ResponseObject);
    }
}

/// <summary>How <see cref="TpayProvider"/> reads a verified notification.</summary>
public class TpayNotificationMappingTests
{
    private static async Task<PaymentWebhookResult> Handle(string body)
    {
        var caller = new Mock<ITpayServiceCaller>();
        caller.Setup(c => c.VerifyNotificationAsync(body, "jws", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return await new TpayProvider(caller.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { TpayProvider.SignatureHeader, "jws" } },
        });
    }

    [Theory]
    [InlineData("TRUE", PaymentStatusEnum.Finished)]
    [InlineData("true", PaymentStatusEnum.Finished)]
    [InlineData("PAID", PaymentStatusEnum.Finished)]
    [InlineData("CHARGEBACK", PaymentStatusEnum.Rejected)]
    [InlineData("chargeback", PaymentStatusEnum.Rejected)]
    [InlineData("FALSE", PaymentStatusEnum.Rejected)]
    public async Task When_Status_Is_Final_Should_Report_It_As_Processed(string status, PaymentStatusEnum expected)
    {
        var result = await Handle($"id=1010&tr_id=TR-1&tr_status={status}");

        var processed = Assert.IsType<PaymentWebhookProcessed>(result);
        Assert.Equal(expected, processed.Payment.PaymentStatus);
        Assert.Equal(TpayProvider.Acknowledgement, processed.Payment.ResponseObject);
    }

    [Theory]
    [InlineData("id=1010&tr_id=TR-1&tr_status=SOMETHING_NEW")]
    [InlineData("id=1010&tr_id=TR-1")]
    public async Task When_Status_Is_Unknown_Or_Absent_Should_Ignore_The_Notification(string body)
    {
        var result = await Handle(body);

        var ignored = Assert.IsType<PaymentWebhookIgnored>(result);
        Assert.Equal(PaymentStatusEnum.Processing, ignored.Payment!.PaymentStatus);
        Assert.Equal("TRUE", ignored.Payment.ResponseObject);
    }

    [Fact]
    public async Task When_Notification_Carries_The_Merchant_Reference_Should_Return_It_As_The_Payment_Id()
    {
        var result = await Handle("id=1010&tr_id=TR-ABC-1&tr_crc=order%2F7+a&tr_status=TRUE");

        Assert.Equal("order/7 a", Assert.IsType<PaymentWebhookProcessed>(result).Payment.PaymentUniqueId);
    }

    [Fact]
    public async Task When_Notification_Has_No_Merchant_Reference_Should_Return_The_Tpay_Transaction_Title()
    {
        var result = await Handle("id=1010&tr_id=TR-ABC-1&tr_crc=&tr_status=TRUE");

        Assert.Equal("TR-ABC-1", Assert.IsType<PaymentWebhookProcessed>(result).Payment.PaymentUniqueId);
    }

    [Fact]
    public async Task When_Verification_Fails_Should_Reject_As_InvalidSignature()
    {
        var result = await new TpayProvider(Mock.Of<ITpayServiceCaller>()).HandleWebhookAsync(new PaymentWebhookRequest { Body = "tr_status=TRUE" });

        Assert.Equal(PaymentWebhookRejection.InvalidSignature, Assert.IsType<PaymentWebhookRejected>(result).Rejection);
    }

    [Fact]
    public async Task When_Called_Through_The_Legacy_Method_Should_Read_The_Signature_From_The_Parameters()
    {
        // arrange
        var caller = new Mock<ITpayServiceCaller>();
        using var cancellation = new CancellationTokenSource();
        caller.Setup(c => c.VerifyNotificationAsync("tr_id=TR-1&tr_status=PAID", "jws", cancellation.Token)).ReturnsAsync(true);

        // act
        var response = await new TpayProvider(caller.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = "tr_id=TR-1&tr_status=PAID",
            QueryParameters = new Dictionary<string, StringValues> { { "X-JWS-Signature", "jws" } },
        }, cancellation.Token);

        // assert
        Assert.Equal(PaymentStatusEnum.Finished, response.PaymentStatus);
        Assert.Equal("TR-1", response.PaymentUniqueId);
    }

    [Fact]
    public async Task When_Legacy_Payload_Is_Null_Should_Reject()
    {
        var response = await new TpayProvider(Mock.Of<ITpayServiceCaller>()).TransactionStatusChange(new TransactionStatusChangePayload());

        Assert.Equal(PaymentStatusEnum.Rejected, response.PaymentStatus);
    }
}

/// <summary>Registration and configuration of the Tpay provider.</summary>
public class TpayRegistrationTests
{
    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string>
        {
            ["Payments:Providers:Tpay:ClientId"] = "client",
            ["Payments:Providers:Tpay:ClientSecret"] = "secret",
            ["Payments:Providers:Tpay:SecurityCode"] = "code",
        };
        foreach (var (key, value) in settings)
            values["Payments:Providers:Tpay:" + key] = value;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.RegisterTpayProvider();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void When_Provider_Is_Registered_Should_Share_One_Caller_So_Certificates_Stay_Cached()
    {
        using var provider = Build();

        Assert.Same(provider.GetRequiredService<ITpayServiceCaller>(), provider.GetRequiredService<ITpayServiceCaller>());
        Assert.IsType<TpayServiceCaller>(provider.GetRequiredService<ITpayServiceCaller>());
    }

    [Fact]
    public void When_Nothing_Is_Configured_Should_Trust_The_Production_Origin()
    {
        using var provider = Build();

        var options = provider.GetRequiredService<IOptions<TpayServiceOptions>>().Value;

        Assert.Equal("https://secure.tpay.com", options.JwsCertificateBaseUrl);
        Assert.Null(options.JwsRootCertificatePem);
    }

    [Fact]
    public void When_Sandbox_Origin_And_Pinned_Root_Are_Configured_Should_Bind_Them()
    {
        using var chain = new TpayCertificateChain();
        using var provider = Build(("JwsCertificateBaseUrl", "https://secure.sandbox.tpay.com"), ("JwsRootCertificatePem", chain.RootBundlePem));

        var options = provider.GetRequiredService<IOptions<TpayServiceOptions>>().Value;

        Assert.Equal("https://secure.sandbox.tpay.com", options.JwsCertificateBaseUrl);
        Assert.Equal(chain.RootBundlePem, options.JwsRootCertificatePem);
    }

    [Theory]
    [InlineData("JwsCertificateBaseUrl", "http://secure.tpay.com")]
    [InlineData("JwsCertificateBaseUrl", "secure.tpay.com")]
    [InlineData("JwsRootCertificatePem", "not a certificate")]
    [InlineData("JwsRootCertificatePem", "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----")]
    public void When_Certificate_Settings_Are_Invalid_Should_Fail_Options_Validation(string key, string value)
    {
        using var provider = Build((key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TpayServiceOptions>>().Value);

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("order-7", "\"hiddenDescription\":\"order-7\"")]
    [InlineData(null, "\"hiddenDescription\":null")]
    [InlineData("  ", "\"hiddenDescription\":null")]
    public async Task When_Transaction_Is_Created_Should_Send_The_Merchant_Reference_As_Hidden_Description(string additionalData, string expected)
    {
        // arrange
        string sent = null;
        var handler = new Mock<HttpMessageHandler>();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new CapturingHandler(body => sent = body)));
        var caller = new TpayServiceCaller(Options.Create(new TpayServiceOptions { ServiceUrl = "https://api.tpay.test" }), factory.Object);

        // act
        var (transactionId, paymentUrl) = await caller.CreateTransactionAsync("token", new PaymentRequest { PaymentProvider = "Tpay", Currency = "PLN", Amount = 10m, Title = "Order", AdditionalData = additionalData });

        // assert
        Assert.Equal("01HZX", transactionId);
        Assert.Equal("https://pay.tpay.test/1", paymentUrl);
        Assert.Contains(expected, sent, StringComparison.Ordinal);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Action<string> capture;

        public CapturingHandler(Action<string> capture) => this.capture = capture;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"transactionId":"01HZX","transactionPaymentUrl":"https://pay.tpay.test/1"}"""),
            };
        }
    }
}
