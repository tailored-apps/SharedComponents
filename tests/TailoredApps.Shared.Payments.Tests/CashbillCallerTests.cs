using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using Xunit;
using CashBillPaymentRequest = TailoredApps.Shared.Payments.Provider.CashBill.PaymentRequest;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>The three calls CashBill needs to start a payment, and the signature each of them carries.</summary>
public class CashbillServiceCallerTests
{
    private const string Secret = "shop-secret";

    private readonly Mock<ICashbillHttpClient> http = new();
    private readonly List<(string Url, string Method, NameValueCollection Form)> calls = [];

    private CashbillServiceCaller CreateSut() => new(http.Object, Options.Create(new CashbillServiceOptions
    {
        ServiceUrl = "https://pay.cashbill.test/ws/rest/",
        ShopId = "shop1",
        ShopSecretPhrase = Secret,
        ReturnUrl = "https://shop.example/return",
        NegativeReturnUrl = "https://shop.example/failed",
    }));

    private static string Sha1(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));

    private static NameValueCollection Read(FormUrlEncodedContent content)
        => content is null ? null : HttpUtility.ParseQueryString(content.ReadAsStringAsync().GetAwaiter().GetResult());

    private void SetupGateway(PaymentStatus status)
    {
        http.Setup(h => h.MakeFormRequest<Payment>(It.IsAny<string>(), "POST", It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, FormUrlEncodedContent, CancellationToken>((url, method, form, _) => calls.Add((url, method, Read(form))))
            .ReturnsAsync(new Payment { Id = "PAY1", RedirectUrl = "https://pay.cashbill.test/redirect/PAY1" });
        http.Setup(h => h.MakeFormRequest(It.IsAny<string>(), "PUT", It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, FormUrlEncodedContent, CancellationToken>((url, method, form, _) => calls.Add((url, method, Read(form))))
            .Returns(Task.CompletedTask);
        http.Setup(h => h.MakeFormRequest<PaymentStatus>(It.IsAny<string>(), "GET", null, It.IsAny<CancellationToken>()))
            .Callback<string, string, FormUrlEncodedContent, CancellationToken>((url, method, form, _) => calls.Add((url, method, Read(form))))
            .ReturnsAsync(status);
    }

    private static CashBillPaymentRequest Request() => new()
    {
        Title = "Order 7",
        Amount = 12.5m,
        Currency = "PLN",
        Description = "Two books",
        AdditionalData = "order-7",
        PaymentChannel = "blik",
        Referer = "shop",
        FirstName = "Jan",
        Surname = "Kowalski",
        Email = "jan@example.com",
        Country = "PL",
        City = "Poznań",
        PostCode = "60-001",
        Street = "Długa",
        House = "1",
        Flat = "2",
    };

    [Fact]
    public async Task When_Payment_Is_Generated_Should_Create_It_Set_Return_Addresses_And_Read_Its_Status()
    {
        // arrange
        SetupGateway(new PaymentStatus { Id = "PAY1", Status = "PreStart" });

        // act
        var status = await CreateSut().GeneratePayment(Request());

        // assert
        Assert.Equal(
            new[]
            {
                "POST https://pay.cashbill.test/ws/rest/payment/shop1",
                "PUT https://pay.cashbill.test/ws/rest/payment/shop1/PAY1",
                "GET https://pay.cashbill.test/ws/rest/payment/shop1/PAY1?sign=" + Sha1("PAY1" + Secret),
            },
            calls.Select(c => $"{c.Method} {c.Url}").ToArray());
        Assert.Equal("PreStart", status.Status);
        Assert.Equal("https://pay.cashbill.test/redirect/PAY1", status.PaymentProviderRedirectUrl);
    }

    [Fact]
    public async Task When_Payment_Is_Created_Should_Send_Every_Field_And_Sign_Them_In_CashBills_Order()
    {
        // arrange
        SetupGateway(new PaymentStatus());
        var request = Request();
        var amount = request.Amount.ToString(CultureInfo.CurrentCulture).Replace(",", ".", StringComparison.Ordinal);

        // act
        await CreateSut().GeneratePayment(request);

        // assert
        var form = calls[0].Form;
        Assert.Equal("Order 7", form["title"]);
        Assert.Equal(amount, form["amount.value"]);
        Assert.Equal("PLN", form["amount.currencyCode"]);
        Assert.Equal("https://shop.example/return", form["returnUrl"]);
        Assert.Equal("https://shop.example/failed", form["negativeReturnUrl"]);
        Assert.Equal("Two books", form["description"]);
        Assert.Equal("order-7", form["additionalData"]);
        Assert.Equal("blik", form["paymentChannel"]);
        Assert.Equal("PL", form["languageCode"]);
        Assert.Equal("shop", form["referer"]);
        Assert.Equal("Jan", form["personalData.firstName"]);
        Assert.Equal("Kowalski", form["personalData.surname"]);
        Assert.Equal("jan@example.com", form["personalData.email"]);
        Assert.Equal("PL", form["personalData.country"]);
        Assert.Equal("Poznań", form["personalData.city"]);
        Assert.Equal("60-001", form["personalData.postcode"]);
        Assert.Equal("Długa", form["personalData.street"]);
        Assert.Equal("1", form["personalData.house"]);
        Assert.Equal("2", form["personalData.flat"]);
        Assert.Equal(
            Sha1("Order 7" + amount + "PLN" + "https://shop.example/return" + "Two books" + "https://shop.example/failed" + "order-7" + "blik" + "PL"
                 + "shop" + "Jan" + "Kowalski" + "jan@example.com" + "PL" + "Poznań" + "60-001" + "Długa" + "1" + "2" + Secret),
            form["sign"]);
    }

    [Fact]
    public async Task When_Return_Addresses_Are_Set_Should_Append_The_Payment_Id_And_Sign_The_Request()
    {
        // arrange
        SetupGateway(new PaymentStatus());

        // act
        await CreateSut().GeneratePayment(Request());

        // assert
        var form = calls[1].Form;
        Assert.Equal("https://shop.example/return/PAY1", form["returnUrl"]);
        Assert.Equal("https://shop.example/failed", form["negativeReturnUrl"]);
        Assert.Equal(Sha1("PAY1" + "https://shop.example/return/PAY1" + "https://shop.example/failed" + Secret), form["sign"]);
    }

    [Fact]
    public async Task When_Optional_Fields_Are_Missing_Should_Sign_Them_As_Empty_Text()
    {
        // arrange
        SetupGateway(new PaymentStatus());
        var request = new CashBillPaymentRequest { Title = "T", Amount = 5m, Currency = "PLN" };

        // act
        await CreateSut().GeneratePayment(request);

        // assert
        var form = calls[0].Form;
        Assert.Equal(Sha1("T" + "5" + "PLN" + "https://shop.example/return" + "https://shop.example/failed" + "PL" + Secret), form["sign"]);
        Assert.Equal(string.Empty, form["personalData.email"]);
    }

    [Fact]
    public async Task When_Payment_Is_Generated_Should_Pass_The_Cancellation_Token_To_Every_Call()
    {
        // arrange
        SetupGateway(new PaymentStatus());
        using var cancellation = new CancellationTokenSource();

        // act
        await CreateSut().GeneratePayment(Request(), cancellation.Token);

        // assert
        http.Verify(h => h.MakeFormRequest<Payment>(It.IsAny<string>(), "POST", It.IsAny<FormUrlEncodedContent>(), cancellation.Token), Times.Once);
        http.Verify(h => h.MakeFormRequest(It.IsAny<string>(), "PUT", It.IsAny<FormUrlEncodedContent>(), cancellation.Token), Times.Once);
        http.Verify(h => h.MakeFormRequest<PaymentStatus>(It.IsAny<string>(), "GET", null, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task When_Creating_The_Payment_Fails_Should_Not_Call_The_Gateway_Again()
    {
        // arrange
        http.Setup(h => h.MakeFormRequest<Payment>(It.IsAny<string>(), "POST", It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebException("gateway down"));

        // act & assert
        await Assert.ThrowsAsync<WebException>(() => CreateSut().GeneratePayment(Request()));
        http.Verify(h => h.MakeFormRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_Status_Is_Requested_Should_Sign_The_Payment_Id()
    {
        // arrange
        SetupGateway(new PaymentStatus { Id = "PAY9", Status = "PositiveFinish" });

        // act
        var status = await CreateSut().GetPaymentStatus("PAY9");

        // assert
        Assert.Equal("PositiveFinish", status.Status);
        Assert.Equal("https://pay.cashbill.test/ws/rest/payment/shop1/PAY9?sign=" + Sha1("PAY9" + Secret), Assert.Single(calls).Url);
    }

    [Theory]
    [InlineData("../other-shop/PAY1")]
    [InlineData("PAY1?sign=x")]
    [InlineData("")]
    public async Task When_Payment_Id_Is_Not_A_Plain_Identifier_Should_Throw_Without_Calling_The_Gateway(string paymentId)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().GetPaymentStatus(paymentId));

        http.VerifyNoOtherCalls();
    }
}

/// <summary>A one-connection-at-a-time HTTP server on the loopback interface that records what it was sent.</summary>
internal sealed class LoopbackHttpServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Task loop;

    public LoopbackHttpServer()
    {
        listener.Start();

        // Not Task.Run: the first accept has to be pending before the constructor returns, so a test
        // that never connects cannot stop the listener ahead of it.
        loop = AcceptAsync();
    }

    public sealed record Request(string Method, string Path, string ContentType, string Body);

    public ConcurrentQueue<Request> Requests { get; } = new();

    public int StatusCode { get; set; } = 200;

    public string ResponseBody { get; set; } = "{}";

    public string Url(string path) => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{path}";

    private async Task AcceptAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping.Token);
            }
            catch (Exception exception) when (exception is InvalidOperationException or SocketException or OperationCanceledException)
            {
                return;
            }

            using (client)
            {
                try
                {
                    await HandleAsync(client.GetStream());
                }
                catch (Exception exception) when (exception is IOException or OperationCanceledException)
                {
                    // the client went away or the server is stopping; the test asserts on what was recorded
                }
            }
        }
    }

    private async Task HandleAsync(NetworkStream stream)
    {
        var head = new StringBuilder();
        var one = new byte[1];
        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(one, stopping.Token) == 0)
                return;
            head.Append((char)one[0]);
        }

        var lines = head.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var requestLine = lines[0].Split(' ');
        var headers = lines.Skip(1)
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (headers.TryGetValue("Expect", out var expect) && expect.Contains("100-continue", StringComparison.OrdinalIgnoreCase))
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"));

        var body = new byte[headers.TryGetValue("Content-Length", out var length) ? int.Parse(length, CultureInfo.InvariantCulture) : 0];
        await stream.ReadExactlyAsync(body, stopping.Token);
        Requests.Enqueue(new Request(requestLine[0], requestLine[1], headers.GetValueOrDefault("Content-Type"), Encoding.UTF8.GetString(body)));

        var payload = Encoding.UTF8.GetBytes(ResponseBody);
        var response = $"HTTP/1.1 {StatusCode} {(StatusCode == 200 ? "OK" : "Error")}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    public void Dispose()
    {
        // Cancelling first releases a connection that is still open, so stopping never waits for a client.
        stopping.Cancel();
        listener.Stop();
        loop.Wait(TimeSpan.FromSeconds(5));
        stopping.Dispose();
    }
}

/// <summary><see cref="CashbillHttpClient"/> against a real HTTP exchange on the loopback interface.</summary>
public class CashbillHttpClientTests
{
    private readonly Mock<ILogger<CashbillHttpClient>> logger = new();

    private CashbillHttpClient CreateSut() => new(logger.Object);

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private void VerifyErrorLogged(Times times)
        => logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);

    [Fact]
    public async Task When_Method_Is_Get_Should_Return_The_Deserialised_Response_Without_Sending_A_Body()
    {
        // arrange
        using var server = new LoopbackHttpServer { ResponseBody = """{"id":"PAY1","status":"PositiveFinish"}""" };

        // act
        var status = await CreateSut().MakeFormRequest<PaymentStatus>(server.Url("payment/shop1/PAY1?sign=abc"), "GET", null);

        // assert
        Assert.Equal("PAY1", status.Id);
        Assert.Equal("PositiveFinish", status.Status);
        var request = Assert.Single(server.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("/payment/shop1/PAY1?sign=abc", request.Path);
        Assert.Equal(string.Empty, request.Body);
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task When_Method_Is_Post_Should_Send_The_Form_And_Return_The_Deserialised_Response()
    {
        // arrange
        using var server = new LoopbackHttpServer { ResponseBody = """{"id":"PAY1","redirectUrl":"https://pay.cashbill.test/redirect/PAY1"}""" };

        // act
        var payment = await CreateSut().MakeFormRequest<Payment>(server.Url("payment/shop1"), "POST", Form(("title", "Zamówienie 7"), ("amount.value", "12.50")));

        // assert
        Assert.Equal("PAY1", payment.Id);
        Assert.Equal("https://pay.cashbill.test/redirect/PAY1", payment.RedirectUrl);
        var request = Assert.Single(server.Requests);
        Assert.Equal("POST", request.Method);
        Assert.StartsWith("application/x-www-form-urlencoded", request.ContentType, StringComparison.Ordinal);
        var form = HttpUtility.ParseQueryString(request.Body);
        Assert.Equal("Zamówienie 7", form["title"]);
        Assert.Equal("12.50", form["amount.value"]);
    }

    [Fact]
    public async Task When_Method_Is_Post_Without_A_Form_Should_Throw_And_Log()
    {
        using var server = new LoopbackHttpServer();

        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().MakeFormRequest<Payment>(server.Url("payment/shop1"), "POST", null));

        Assert.Empty(server.Requests);
        VerifyErrorLogged(Times.Once());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task When_Gateway_Answers_With_Json_Null_Should_Throw_Instead_Of_Returning_Null(string method)
    {
        using var server = new LoopbackHttpServer { ResponseBody = "null" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().MakeFormRequest<Payment>(server.Url("payment/shop1"), method, method == "GET" ? null : Form(("a", "b"))));

        Assert.Contains("empty response", exception.Message, StringComparison.Ordinal);
        VerifyErrorLogged(Times.Once());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task When_Gateway_Answers_With_An_Error_Status_Should_Throw_And_Log(string method)
    {
        using var server = new LoopbackHttpServer { StatusCode = 500, ResponseBody = """{"error":"boom"}""" };

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => CreateSut().MakeFormRequest<Payment>(server.Url("payment/shop1"), method, method == "GET" ? null : Form(("a", "b"))));

        Assert.Equal("CashBill", exception.Provider);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal("""{"error":"boom"}""", exception.ResponseBody);
        Assert.True(exception.IsTransient);
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task When_Address_Is_Not_A_Url_Should_Throw_And_Log()
    {
        await Assert.ThrowsAsync<UriFormatException>(() => CreateSut().MakeFormRequest<Payment>("not a url", "GET", null));
        await Assert.ThrowsAsync<UriFormatException>(() => CreateSut().MakeFormRequest("not a url", "PUT", Form(("a", "b"))));

        VerifyErrorLogged(Times.Exactly(2));
    }

    [Fact]
    public async Task When_Request_Has_No_Typed_Response_Should_Send_The_Form_And_Complete()
    {
        // arrange
        using var server = new LoopbackHttpServer { ResponseBody = string.Empty };

        // act
        await CreateSut().MakeFormRequest(server.Url("payment/shop1/PAY1"), "PUT", Form(("returnUrl", "https://shop.example/return/PAY1"), ("sign", "abc")));

        // assert
        var request = Assert.Single(server.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.Equal("/payment/shop1/PAY1", request.Path);
        var form = HttpUtility.ParseQueryString(request.Body);
        Assert.Equal("https://shop.example/return/PAY1", form["returnUrl"]);
        Assert.Equal("abc", form["sign"]);
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task When_Request_Without_Typed_Response_Fails_Should_Throw_And_Log()
    {
        using var server = new LoopbackHttpServer { StatusCode = 403, ResponseBody = "denied" };

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => CreateSut().MakeFormRequest(server.Url("payment/shop1/PAY1"), "PUT", Form(("sign", "bad"))));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.False(exception.IsTransient);
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task When_The_Token_Is_Cancelled_Should_Not_Start_A_Request_Without_Typed_Response()
    {
        using var server = new LoopbackHttpServer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateSut().MakeFormRequest(server.Url("payment/shop1/PAY1"), "PUT", Form(("a", "b")), cancellation.Token));

        Assert.Empty(server.Requests);
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task When_Sent_Should_Keep_The_Content_Type_CashBill_Has_Always_Received()
    {
        using var server = new LoopbackHttpServer();

        await CreateSut().MakeFormRequest(server.Url("payment/shop1/PAY1"), "PUT", Form(("a", "b")));

        Assert.Equal("application/x-www-form-urlencoded; charset=UTF8", Assert.Single(server.Requests).ContentType);
    }

    [Fact]
    public async Task When_A_Request_Fails_Should_Log_The_Address_Without_The_Signature_In_The_Query()
    {
        // arrange
        using var server = new LoopbackHttpServer { StatusCode = 503 };
        var logged = new List<string>();
        logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()))
              .Callback(new InvocationAction(invocation => logged.Add(invocation.Arguments[2].ToString())));

        // act
        await Assert.ThrowsAsync<PaymentGatewayException>(
            () => CreateSut().MakeFormRequest<PaymentStatus>(server.Url("payment/shop1/PAY1?sign=0123456789abcdef"), "GET", null));

        // assert
        var message = Assert.Single(logged);
        Assert.Contains("/payment/shop1/PAY1", message, StringComparison.Ordinal);
        Assert.Contains("GET", message, StringComparison.Ordinal);
        Assert.DoesNotContain("0123456789abcdef", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sign=", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_Built_With_A_Factory_Should_Send_Through_The_Named_Client()
    {
        // arrange
        using var server = new LoopbackHttpServer { ResponseBody = """{"id":"PAY1"}""" };
        var names = new List<string>();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns((string name) =>
        {
            names.Add(name);
            return new HttpClient();
        });
        var sut = new CashbillHttpClient(logger.Object, factory.Object);

        // act
        var first = await sut.MakeFormRequest<PaymentStatus>(server.Url("payment/shop1/PAY1"), "GET", null);
        var second = await sut.MakeFormRequest<PaymentStatus>(server.Url("payment/shop1/PAY1"), "GET", null);

        // assert
        Assert.Equal("PAY1", first.Id);
        Assert.Equal("PAY1", second.Id);
        Assert.Equal(new[] { "CashBill", "CashBill" }, names);
        Assert.Equal(CashbillHttpClient.HttpClientName, names[0]);
    }

    [Fact]
    public void When_Factory_Is_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CashbillHttpClient(logger.Object, null));
    }

    [Fact]
    public void When_Provider_Is_Registered_Should_Resolve_The_Client_With_The_Http_Client_Factory()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterCashbillProvider();
        using var provider = services.BuildServiceProvider();

        // act
        var client = provider.GetRequiredService<ICashbillHttpClient>();

        // assert
        Assert.IsType<CashbillHttpClient>(client);
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>().CreateClient(CashbillHttpClient.HttpClientName));
    }
}
