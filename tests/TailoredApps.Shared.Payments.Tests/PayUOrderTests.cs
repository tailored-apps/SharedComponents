using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Payments.Provider.PayU;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// PayU answers an order with a 302 whose body holds the order id. Orders are created through the named
/// HTTP client <c>PayU.Orders</c>, which must not follow that redirect.
/// </summary>
public class PayUOrderTests
{
    private const string OrdersUrl = "https://secure.payu.test/api/v2_1/orders";

    private sealed class Gateway : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> answer;

        public Gateway(Func<HttpRequestMessage, HttpResponseMessage> answer) => this.answer = answer;

        public List<(Uri Uri, string Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            return answer(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string location = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (location is not null)
            response.Headers.Location = new Uri(location);
        return response;
    }

    private static (PayUServiceCaller Caller, List<string> ClientNames) Caller(Gateway gateway)
    {
        var names = new List<string>();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns((string name) =>
               {
                   names.Add(name);
                   return new HttpClient(gateway, disposeHandler: false);
               });
        var caller = new PayUServiceCaller(Options.Create(new PayUServiceOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            PosId = "pos-1",
            SignatureKey = "key",
            ServiceUrl = "https://secure.payu.test",
            NotifyUrl = "https://shop.example/notify",
            ContinueUrl = "https://shop.example/continue",
        }), factory.Object);
        return (caller, names);
    }

    private static PaymentRequest Request() => new("PayU", 12.34m, "pln") { Title = "Order 7", Email = "buyer@example.com" };

    [Fact]
    public async Task When_PayU_Answers_With_A_Redirect_Should_Return_The_Order_Id_And_The_Payment_Page()
    {
        // arrange
        var gateway = new Gateway(_ => Json(HttpStatusCode.Found, """{"orderId":"ORD-1","redirectUri":"https://secure.payu.test/pay/body"}""", "https://secure.payu.test/pay/header"));
        var (caller, _) = Caller(gateway);

        // act
        var (orderId, redirectUri, error) = await caller.CreateOrderAsync("token-1", Request());

        // assert
        Assert.Equal("ORD-1", orderId);
        Assert.Equal("https://secure.payu.test/pay/header", redirectUri);
        Assert.Null(error);
    }

    [Fact]
    public async Task When_Order_Is_Created_Should_Use_The_Orders_Client_With_The_Token_And_Amount_In_Minor_Units()
    {
        // arrange
        var gateway = new Gateway(_ => Json(HttpStatusCode.Found, """{"orderId":"ORD-1"}""", "https://secure.payu.test/pay/1"));
        var (caller, clientNames) = Caller(gateway);

        // act
        await caller.CreateOrderAsync("token-1", Request());

        // assert
        Assert.Equal(new[] { "PayU.Orders" }, clientNames);
        Assert.Equal(PayUServiceCaller.OrdersClientName, clientNames[0]);
        var (uri, authorization, body) = Assert.Single(gateway.Requests);
        Assert.Equal(OrdersUrl, uri.AbsoluteUri);
        Assert.Equal("Bearer token-1", authorization);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("1234", sent.RootElement.GetProperty("totalAmount").GetString());
        Assert.Equal("PLN", sent.RootElement.GetProperty("currencyCode").GetString());
        Assert.Equal("pos-1", sent.RootElement.GetProperty("merchantPosId").GetString());
    }

    [Fact]
    public async Task When_PayU_Answers_With_Success_And_No_Redirect_Should_Take_The_Address_From_The_Body()
    {
        var gateway = new Gateway(_ => Json(HttpStatusCode.OK, """{"orderId":"ORD-2","redirectUri":"https://secure.payu.test/pay/2"}"""));

        var (orderId, redirectUri, error) = await Caller(gateway).Caller.CreateOrderAsync("token", Request());

        Assert.Equal("ORD-2", orderId);
        Assert.Equal("https://secure.payu.test/pay/2", redirectUri);
        Assert.Null(error);
    }

    [Fact]
    public async Task When_PayU_Refuses_The_Order_Should_Report_Rejected_With_The_Reason()
    {
        // arrange
        var gateway = new Gateway(_ => Json(HttpStatusCode.BadRequest, """{"status":{"statusCode":"ERROR_VALUE_INVALID","statusDesc":"Invalid currency"}}"""));
        var (caller, _) = Caller(gateway);

        // act
        var (orderId, _, error) = await caller.CreateOrderAsync("token", Request());

        // assert
        Assert.Null(orderId);
        Assert.Contains("ERROR_VALUE_INVALID", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task When_PayU_Cannot_Handle_The_Order_Should_Throw_Instead_Of_Reporting_Rejected(HttpStatusCode status)
    {
        var gateway = new Gateway(_ => Json(status, "<html>upstream error</html>"));

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => Caller(gateway).Caller.CreateOrderAsync("token", Request()));

        Assert.Equal("PayU", exception.Provider);
        Assert.Equal(status, exception.StatusCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task When_The_Client_Followed_The_Redirect_Should_Throw_Instead_Of_Losing_The_Order_Id()
    {
        // arrange: what a client with AllowAutoRedirect = true hands back - the payment page, fetched from another address
        var gateway = new Gateway(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>pay here</html>"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://secure.payu.test/pay/abc?token=secret"),
        });

        // act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Caller(gateway).Caller.CreateOrderAsync("token", Request()));

        // assert
        Assert.Contains("PayU.Orders", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    private static HttpMessageHandler PrimaryHandler(IServiceProvider services, string clientName)
    {
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        while (handler is DelegatingHandler delegating && delegating.InnerHandler is not null)
            handler = delegating.InnerHandler;
        return handler;
    }

    [Fact]
    public void When_Provider_Is_Registered_Should_Configure_The_Orders_Client_Not_To_Follow_Redirects_Or_Keep_Cookies()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterPayUProvider();
        using var provider = services.BuildServiceProvider();

        // act
        var orders = Assert.IsType<SocketsHttpHandler>(PrimaryHandler(provider, PayUServiceCaller.OrdersClientName));

        // assert
        Assert.False(orders.AllowAutoRedirect);
        Assert.False(orders.UseCookies);
    }
}
