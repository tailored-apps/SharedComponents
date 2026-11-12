using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Payments.Provider.Adyen;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Adyen Hosted Checkout: a session created with <c>mode: hosted</c> returns the address of the payment
/// page, and its outcome is read from <c>GET /sessions/{id}?sessionResult=…</c>.
/// </summary>
public class AdyenSessionTests
{
    private sealed class RecordingGateway : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string body;

        public RecordingGateway(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            this.body = body;
            this.status = status;
        }

        public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!, sent));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static AdyenServiceCaller Caller(RecordingGateway gateway)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(gateway, disposeHandler: false));
        return new AdyenServiceCaller(Options.Create(new AdyenServiceOptions
        {
            ApiKey = "key",
            MerchantAccount = "Shop",
            ReturnUrl = "https://shop.example/return",
            CheckoutUrl = "https://checkout.adyen.test/v71",
        }), factory.Object);
    }

    private static PaymentRequest Request() => new("Adyen", 49.99m, "pln") { Email = "buyer@example.com", AdditionalData = "order-7" };

    // ─── Creating the session ────────────────────────────────────────────────

    [Fact]
    public async Task When_Payment_Is_Requested_Should_Create_A_Hosted_Session_And_Return_Its_Page_Address()
    {
        // arrange
        var gateway = new RecordingGateway("""{"id":"CS451F2AB1","url":"https://test.adyen.link/PL1234","sessionData":"Ab02b4c0!","mode":"hosted"}""");

        // act
        var response = await new AdyenProvider(Caller(gateway)).RequestPayment(Request());

        // assert
        Assert.Equal(PaymentStatusEnum.Created, response.PaymentStatus);
        Assert.Equal("CS451F2AB1", response.PaymentUniqueId);
        Assert.Equal("https://test.adyen.link/PL1234", response.RedirectUrl);
    }

    [Fact]
    public async Task When_Session_Is_Created_Should_Ask_For_Hosted_Mode_With_Amount_In_Minor_Units()
    {
        // arrange
        var gateway = new RecordingGateway("""{"id":"CS451F2AB1","url":"https://test.adyen.link/PL1234"}""");

        // act
        await Caller(gateway).CreateSessionAsync(Request());

        // assert
        var (method, uri, body) = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://checkout.adyen.test/v71/sessions", uri.AbsoluteUri);
        using var sent = JsonDocument.Parse(body);
        var root = sent.RootElement;
        Assert.Equal("hosted", root.GetProperty("mode").GetString());
        Assert.Equal("Shop", root.GetProperty("merchantAccount").GetString());
        Assert.Equal("order-7", root.GetProperty("reference").GetString());
        Assert.Equal("https://shop.example/return", root.GetProperty("returnUrl").GetString());
        Assert.Equal(4999, root.GetProperty("amount").GetProperty("value").GetInt64());
        Assert.Equal("PLN", root.GetProperty("amount").GetProperty("currency").GetString());
    }

    [Theory]
    [InlineData("live", null, false, null)]
    [InlineData("test", "https://checkout.example/v71", true, "https://checkout.example/v71")]
    public void When_Options_Are_Bound_Should_Read_The_Environment_And_The_Checkout_Address(string environment, string checkoutUrl, bool isTest, string expectedUrl)
    {
        // arrange
        var settings = new Dictionary<string, string> { ["Payments:Providers:Adyen:Environment"] = environment };
        if (checkoutUrl is not null)
            settings["Payments:Providers:Adyen:CheckoutUrl"] = checkoutUrl;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new AdyenServiceOptions();

        // act
        new AdyenConfigureOptions(configuration).Configure(options);

        // assert
        Assert.Equal(environment, options.Environment);
        Assert.Equal(isTest, options.IsTest);
        Assert.Equal(expectedUrl, options.CheckoutUrl);
    }

    // ─── Reading the outcome ─────────────────────────────────────────────────

    [Theory]
    [InlineData("completed", PaymentStatusEnum.Finished)]
    [InlineData("paymentPending", PaymentStatusEnum.Processing)]
    [InlineData("refused", PaymentStatusEnum.Rejected)]
    [InlineData("canceled", PaymentStatusEnum.Rejected)]
    [InlineData("expired", PaymentStatusEnum.Rejected)]
    [InlineData("active", PaymentStatusEnum.Created)]
    [InlineData("somethingNew", PaymentStatusEnum.Created)]
    public async Task When_Session_Result_Is_Read_Should_Map_The_Session_Status(string sessionStatus, PaymentStatusEnum expected)
    {
        var gateway = new RecordingGateway($$"""{"id":"CS451F2AB1","status":"{{sessionStatus}}","reference":"order-7"}""");

        var status = await Caller(gateway).GetPaymentStatusAsync(AdyenProvider.SessionResultId("CS451F2AB1", "result"));

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task When_Session_Result_Is_Read_Should_Query_The_Session_With_The_Escaped_Session_Result()
    {
        // arrange: sessionResult is opaque base64-like text that needs escaping in a query string
        var gateway = new RecordingGateway("""{"status":"completed"}""");

        // act
        await Caller(gateway).GetPaymentStatusAsync(AdyenProvider.SessionResultId("CS451F2AB1", "X3XtfGC9!H4sIAA+/abc=="));

        // assert
        var (method, uri, _) = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal("https://checkout.adyen.test/v71/sessions/CS451F2AB1?sessionResult=X3XtfGC9%21H4sIAA%2B%2Fabc%3D%3D", uri.AbsoluteUri);
    }

    [Fact]
    public async Task When_Provider_Status_Is_Read_Should_Return_The_Session_Id_Without_The_Session_Result()
    {
        var gateway = new RecordingGateway("""{"status":"completed"}""");

        var response = await new AdyenProvider(Caller(gateway)).GetStatus(AdyenProvider.SessionResultId("CS451F2AB1", "result"));

        Assert.Equal("CS451F2AB1", response.PaymentUniqueId);
        Assert.Equal(PaymentStatusEnum.Finished, response.PaymentStatus);
    }

    [Theory]
    [InlineData("CS451F2AB1")]
    [InlineData("CS451F2AB1|")]
    public async Task When_Payment_Id_Has_No_Session_Result_Should_Throw_Without_Calling_Adyen(string paymentId)
    {
        // arrange
        var gateway = new RecordingGateway("""{"status":"completed"}""");

        // act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => Caller(gateway).GetPaymentStatusAsync(paymentId));

        // assert
        Assert.Contains("sessionResult", exception.Message, StringComparison.Ordinal);
        Assert.Contains("webhook", exception.Message, StringComparison.Ordinal);
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [InlineData("../payments|result")]
    [InlineData("CS1/extra|result")]
    public async Task When_Session_Id_Is_Not_A_Plain_Identifier_Should_Throw_Without_Calling_Adyen(string paymentId)
    {
        var gateway = new RecordingGateway("""{"status":"completed"}""");

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Caller(gateway).GetPaymentStatusAsync(paymentId));

        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public void When_Session_Result_Id_Is_Built_Should_Join_Both_Parts_And_Refuse_Blank_Ones()
    {
        Assert.Equal("CS451F2AB1|result", AdyenProvider.SessionResultId("CS451F2AB1", "result"));
        Assert.ThrowsAny<ArgumentException>(() => AdyenProvider.SessionResultId(" ", "result"));
        Assert.ThrowsAny<ArgumentException>(() => AdyenProvider.SessionResultId("CS451F2AB1", ""));
        Assert.ThrowsAny<ArgumentException>(() => AdyenProvider.SessionResultId(null, "result"));
    }
}
