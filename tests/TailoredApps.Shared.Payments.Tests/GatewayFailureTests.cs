using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.HotPay;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Provider.Przelewy24;
using TailoredApps.Shared.Payments.Provider.Revolut;
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// A gateway that is failing must not look like a payment that was rejected: providers throw
/// <see cref="PaymentGatewayException"/> instead of returning <see cref="PaymentStatusEnum.Rejected"/>.
/// </summary>
public class GatewayFailureTests
{
    private sealed class FixedAnswer : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string body;

        public FixedAnswer(HttpStatusCode status, string body)
        {
            this.status = status;
            this.body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private static IHttpClientFactory Gateway(HttpStatusCode status, string body = """{"error":"gateway"}""")
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new FixedAnswer(status, body)));
        return factory.Object;
    }

    private static PaymentRequest Request(string provider) => new()
    {
        PaymentProvider = provider,
        Amount = 10m,
        Currency = "PLN",
        Title = "Order 7",
        Email = "buyer@example.com",
        AdditionalData = "order-7",
    };

    private static AdyenServiceCaller Adyen(IHttpClientFactory factory)
        => new(Options.Create(new AdyenServiceOptions { ApiKey = "key", MerchantAccount = "Shop", CheckoutUrl = "https://checkout.adyen.test/v71" }), factory);

    private static PayNowServiceCaller PayNow(IHttpClientFactory factory)
        => new(Options.Create(new PayNowServiceOptions { ApiKey = "key", SignatureKey = "sig", ServiceUrl = "https://api.paynow.test" }), factory);

    private static PayUServiceCaller PayU(IHttpClientFactory factory)
        => new(Options.Create(new PayUServiceOptions { ClientId = "client", ClientSecret = "secret", PosId = "pos", ServiceUrl = "https://payu.test" }), factory);

    private static Przelewy24ServiceCaller Przelewy24(IHttpClientFactory factory)
        => new(Options.Create(new Przelewy24ServiceOptions { MerchantId = 1, PosId = 1, ApiKey = "key", CrcKey = "crc", ServiceUrl = "https://p24.test" }), factory);

    private static RevolutServiceCaller Revolut(IHttpClientFactory factory)
        => new(Options.Create(new RevolutServiceOptions { ApiKey = "key", ApiUrl = "https://revolut.test/api", WebhookSecret = "whsec" }), factory);

    private static HotPayServiceCaller HotPay(IHttpClientFactory factory)
        => new(Options.Create(new HotPayServiceOptions { SecretHash = "secret", ServiceUrl = "https://hotpay.test", ReturnUrl = "https://shop.example/return" }), factory);

    private static TpayServiceCaller Tpay(IHttpClientFactory factory)
        => new(Options.Create(new TpayServiceOptions { ClientId = "client", ClientSecret = "secret", SecurityCode = "code", ServiceUrl = "https://tpay.test" }), factory);

    /// <summary>The status query of each provider, by provider key.</summary>
    private static Task QueryStatus(string provider, IHttpClientFactory factory) => provider switch
    {
        "Adyen" => Adyen(factory).GetPaymentStatusAsync("psp_1"),
        "PayNow" => PayNow(factory).GetPaymentStatusAsync("pn_1"),
        "PayU" => PayU(factory).GetOrderStatusAsync("token", "order_1"),
        "Revolut" => Revolut(factory).GetOrderAsync("rev_1"),
        "Tpay" => Tpay(factory).GetTransactionStatusAsync("token", "txn_1"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    /// <summary>The call that creates a payment in each provider, by provider key.</summary>
    private static Task CreatePayment(string provider, IHttpClientFactory factory) => provider switch
    {
        "Adyen" => Adyen(factory).CreateSessionAsync(Request(provider)),
        "PayNow" => PayNow(factory).CreatePaymentAsync(Request(provider)),
        "Przelewy24" => Przelewy24(factory).RegisterTransactionAsync(Request(provider), "session-1"),
        "Revolut" => Revolut(factory).CreateOrderAsync(Request(provider)),
        "HotPay" => HotPay(factory).InitPaymentAsync(Request(provider), "payment-1"),
        "Tpay" => Tpay(factory).CreateTransactionAsync("token", Request(provider)),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static IEnumerable<object[]> StatusQueryFailures()
    {
        foreach (var provider in new[] { "Adyen", "PayNow", "PayU", "Revolut", "Tpay" })
        {
            foreach (var status in new[] { HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized })
                yield return [provider, status];
        }
    }

    public static IEnumerable<object[]> CreateFailures()
    {
        foreach (var provider in new[] { "Adyen", "PayNow", "Przelewy24", "Revolut", "HotPay", "Tpay" })
        {
            foreach (var status in new[] { HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests })
                yield return [provider, status];
        }
    }

    // ─── Status queries ──────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(StatusQueryFailures))]
    public async Task When_Status_Query_Is_Not_Answered_With_Success_Should_Throw_Instead_Of_Reporting_Rejected(string provider, HttpStatusCode status)
    {
        // act
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => QueryStatus(provider, Gateway(status)));

        // assert
        Assert.Equal(provider, exception.Provider);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("""{"error":"gateway"}""", exception.ResponseBody);
    }

    [Fact]
    public async Task When_Status_Query_Fails_Should_Be_Catchable_As_HttpRequestException()
    {
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => QueryStatus("Tpay", Gateway(HttpStatusCode.BadGateway)));
    }

    [Fact]
    public async Task When_Provider_Status_Is_Read_Should_Let_The_Gateway_Failure_Reach_The_Host()
    {
        // arrange
        var provider = new AdyenProvider(Adyen(Gateway(HttpStatusCode.ServiceUnavailable)));

        // act & assert
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.GetStatus("psp_1"));
        Assert.True(exception.IsTransient);
    }

    // ─── Creating a payment ──────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(CreateFailures))]
    public async Task When_Gateway_Cannot_Handle_Payment_Creation_Should_Throw_Instead_Of_Reporting_Rejected(string provider, HttpStatusCode status)
    {
        // act: an HTML error page, as a reverse proxy in front of a failing gateway answers
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => CreatePayment(provider, Gateway(status, "<html>upstream error</html>")));

        // assert
        Assert.Equal(provider, exception.Provider);
        Assert.Equal(status, exception.StatusCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task When_Gateway_Refuses_Payment_Creation_Should_Still_Report_Rejected_With_The_Reason()
    {
        // arrange: a 4xx is the gateway saying no to this request, not a gateway failure
        var provider = new AdyenProvider(Adyen(Gateway(HttpStatusCode.UnprocessableEntity, """{"errorCode":"130","message":"Required field 'reference' is not provided."}""")));

        // act
        var response = await provider.RequestPayment(Request("Adyen"));

        // assert
        Assert.Equal(PaymentStatusEnum.Rejected, response.PaymentStatus);
        Assert.Contains("reference", Assert.IsType<string>(response.ResponseObject), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Tpay")]
    [InlineData("PayU")]
    public async Task When_Access_Token_Is_Refused_Should_Throw_PaymentGatewayException(string provider)
    {
        // arrange
        var factory = Gateway(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""");

        // act
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => provider == "Tpay" ? Tpay(factory).GetAccessTokenAsync() : PayU(factory).GetAccessTokenAsync());

        // assert
        Assert.Equal(provider, exception.Provider);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.False(exception.IsTransient);
    }

    // ─── Przelewy24 verification ─────────────────────────────────────────────

    [Fact]
    public async Task When_Przelewy24_Refuses_The_Verification_Should_Report_Rejected()
    {
        var status = await Przelewy24(Gateway(HttpStatusCode.BadRequest)).VerifyTransactionAsync("session-1", 1000, "PLN", 5);

        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task When_Przelewy24_Cannot_Verify_Should_Throw_Instead_Of_Reporting_Rejected()
    {
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => Przelewy24(Gateway(HttpStatusCode.ServiceUnavailable)).VerifyTransactionAsync("session-1", 1000, "PLN", 5));

        Assert.Equal("Przelewy24", exception.Provider);
    }

    [Fact]
    public async Task When_Przelewy24_Cannot_Verify_During_A_Notification_Should_Reject_It_So_It_Is_Sent_Again()
    {
        // arrange
        const string body = """{"sessionId":"session-1","amount":1000,"currency":"PLN","orderId":5}""";
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(body)).Returns(true);
        caller.Setup(c => c.VerifyTransactionAsync("session-1", 1000, "PLN", 5, It.IsAny<CancellationToken>()))
              .ThrowsAsync(new PaymentGatewayException("Przelewy24", HttpStatusCode.ServiceUnavailable));
        var provider = new Przelewy24Provider(caller.Object, Options.Create(new Przelewy24ServiceOptions()));

        // act
        var result = await provider.HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        // assert: not "processed as rejected" - the payment may well have succeeded
        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(PaymentWebhookRejection.ProcessingFailed, rejected.Rejection);
        Assert.Contains("503", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_Przelewy24_Notification_Is_Cancelled_Should_Propagate_The_Cancellation()
    {
        // arrange
        const string body = """{"sessionId":"session-1","amount":1000,"currency":"PLN","orderId":5}""";
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(body)).Returns(true);
        caller.Setup(c => c.VerifyTransactionAsync("session-1", 1000, "PLN", 5, It.IsAny<CancellationToken>()))
              .ThrowsAsync(new OperationCanceledException());
        var provider = new Przelewy24Provider(caller.Object, Options.Create(new Przelewy24ServiceOptions()));

        // act & assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.HandleWebhookAsync(new PaymentWebhookRequest { Body = body }));
    }

    // ─── The exception itself ────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.OK, false)]
    public void When_Status_Is_Classified_Should_Treat_Only_5xx_408_And_429_As_Unavailable(HttpStatusCode status, bool unavailable)
    {
        Assert.Equal(unavailable, PaymentGatewayException.IsUnavailable(status));
        Assert.Equal(unavailable, new PaymentGatewayException("Tpay", status).IsTransient);
    }

    [Fact]
    public void When_Created_Should_Name_The_Provider_And_Status_But_Keep_The_Body_Out_Of_The_Message()
    {
        var exception = new PaymentGatewayException("Tpay", HttpStatusCode.ServiceUnavailable, "card 4111 1111 1111 1111 declined");

        Assert.Contains("'Tpay'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("503", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("4111", exception.Message, StringComparison.Ordinal);
        Assert.Equal("card 4111 1111 1111 1111 declined", exception.ResponseBody);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public void When_Body_Is_Long_Should_Keep_Only_Its_Beginning()
    {
        var exception = new PaymentGatewayException("Tpay", HttpStatusCode.BadGateway, new string('x', 10_000));

        Assert.Equal(PaymentGatewayException.MaxResponseBodyLength, exception.ResponseBody!.Length);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task When_Gateway_Answered_Or_Refused_Should_Not_Throw_As_Unavailable(HttpStatusCode status)
    {
        using var response = new HttpResponseMessage(status);

        await PaymentGatewayException.ThrowIfUnavailableAsync("Tpay", response);
    }

    [Fact]
    public async Task When_Success_Is_Required_Should_Throw_For_A_Refusal_And_Pass_For_Success()
    {
        using var ok = new HttpResponseMessage(HttpStatusCode.NoContent);
        using var refused = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("denied") };

        await PaymentGatewayException.ThrowIfNotSuccessAsync("Tpay", ok);
        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => PaymentGatewayException.ThrowIfNotSuccessAsync("Tpay", refused));

        Assert.Equal("denied", exception.ResponseBody);
        await Assert.ThrowsAsync<ArgumentNullException>(() => PaymentGatewayException.ThrowIfNotSuccessAsync("Tpay", null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PaymentGatewayException.ThrowIfUnavailableAsync("Tpay", null));
    }
}
