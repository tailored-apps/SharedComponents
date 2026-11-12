#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Security;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

public class WebhookSignatureComparisonTests
{
    [Fact]
    public void When_Values_Are_Identical_Should_Return_True()
    {
        Assert.True(WebhookSignature.FixedTimeEquals("a1B2c3", "a1B2c3"));
    }

    [Fact]
    public void When_Values_Differ_In_One_Character_Should_Return_False()
    {
        Assert.False(WebhookSignature.FixedTimeEquals("a1b2c3", "a1b2c4"));
    }

    [Fact]
    public void When_Values_Differ_In_Length_Should_Return_False()
    {
        Assert.False(WebhookSignature.FixedTimeEquals("a1b2c3", "a1b2c"));
    }

    [Fact]
    public void When_Values_Differ_Only_By_Case_And_Case_Is_Significant_Should_Return_False()
    {
        Assert.False(WebhookSignature.FixedTimeEquals("AbCdEf", "abcdef"));
    }

    [Fact]
    public void When_Values_Differ_Only_By_Case_And_Case_Is_Ignored_Should_Return_True()
    {
        Assert.True(WebhookSignature.FixedTimeEqualsIgnoreCase("deadbeef", "DEADBEEF"));
    }

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    [InlineData("", "abc")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    public void When_Either_Value_Is_Missing_Should_Return_False(string? expected, string? actual)
    {
        Assert.False(WebhookSignature.FixedTimeEquals(expected, actual));
        Assert.False(WebhookSignature.FixedTimeEqualsIgnoreCase(expected, actual));
    }
}

public class CashBillWebhookSignatureTests
{
    private const string ExpectedSign = "0123456789abcdef0123456789abcdef";

    private static PaymentWebhookRequest Request(string sign) => new()
    {
        Query = new Dictionary<string, StringValues>
        {
            { "cmd", "transactionStatusChanged" },
            { "args", "TX-1" },
            { "sign", sign },
        },
    };

    private static Mock<ICashbillServiceCaller> Caller()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>(), It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedSign);
        caller.Setup(c => c.GetPaymentStatus("TX-1", It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentStatus { Id = "TX-1", Status = "PositiveFinish" });
        return caller;
    }

    [Fact]
    public async Task When_Signature_Is_Invalid_Should_Fail_Without_Revealing_Expected_Signature()
    {
        var caller = Caller();
        var provider = new CashBillProvider(caller.Object);

        var result = await provider.HandleWebhookAsync(Request("ffffffffffffffffffffffffffffffff"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain(ExpectedSign, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        caller.Verify(c => c.GetPaymentStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_Signature_Is_Missing_Should_Fail()
    {
        var caller = Caller();
        var provider = new CashBillProvider(caller.Object);

        var result = await provider.HandleWebhookAsync(Request(string.Empty));

        Assert.False(result.Success);
        caller.Verify(c => c.GetPaymentStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_Signature_Matches_In_Different_Case_Should_Accept_And_Poll_Status()
    {
        var caller = Caller();
        var provider = new CashBillProvider(caller.Object);

        var result = await provider.HandleWebhookAsync(Request(ExpectedSign.ToUpperInvariant()));

        Assert.True(result.Success);
        Assert.Equal("TX-1", result.PaymentResponse?.PaymentUniqueId);
        caller.Verify(c => c.GetPaymentStatus("TX-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class PayUCreateOrderRedirectTests
{
    private const string RedirectTarget = "https://secure.payu.example/pay/?orderId=ORD-1";

    /// <summary>
    /// Minimal loopback HTTP server: answers every request with the 302 that PayU returns for a new
    /// order, and counts requests so the test can tell whether the client followed the redirect.
    /// </summary>
    private sealed class RedirectingServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task loop;

        public RedirectingServer()
        {
            listener.Start();

            // Not Task.Run: the first accept has to be pending before the constructor returns.
            loop = AcceptLoopAsync();
        }

        public int Requests { get; private set; }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (Exception e) when (e is InvalidOperationException or SocketException)
                {
                    return;
                }

                using (client)
                {
                    var stream = client.GetStream();
                    await ReadRequestAsync(stream);
                    Requests++;

                    var body = Encoding.UTF8.GetBytes("{\"orderId\":\"ORD-1\",\"status\":{\"statusCode\":\"SUCCESS\"}}");
                    var head = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 302 Found\r\n" +
                        $"Location: {RedirectTarget}\r\n" +
                        "Content-Type: application/json\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Connection: close\r\n\r\n");
                    await stream.WriteAsync(head);
                    await stream.WriteAsync(body);
                    await stream.FlushAsync();
                }
            }
        }

        private static async Task ReadRequestAsync(Stream stream)
        {
            var buffer = new byte[8192];
            var received = new MemoryStream();
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return;
                }

                received.Write(buffer, 0, read);
                var text = Encoding.ASCII.GetString(received.GetBuffer(), 0, (int)received.Length);
                var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd < 0)
                {
                    continue;
                }

                var contentLength = 0;
                foreach (var line in text[..headerEnd].Split("\r\n"))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(line["Content-Length:".Length..].Trim());
                    }
                }

                if (received.Length >= headerEnd + 4 + contentLength)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            listener.Stop();
            loop.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>The caller exactly as a host gets it: from <c>RegisterPayUProvider()</c> and the real HTTP client factory.</summary>
    private static (ServiceProvider Services, IPayUServiceCaller Caller) Build(string serviceUrl)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payments:Providers:PayU:ClientId"] = "client",
            ["Payments:Providers:PayU:ClientSecret"] = "secret",
            ["Payments:Providers:PayU:PosId"] = "pos123",
            ["Payments:Providers:PayU:SignatureKey"] = "sigkey",
            ["Payments:Providers:PayU:ServiceUrl"] = serviceUrl,
            ["Payments:Providers:PayU:NotifyUrl"] = "https://example.com/notify",
            ["Payments:Providers:PayU:ContinueUrl"] = "https://example.com/continue",
        }).Build());
        services.RegisterPayUProvider();
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<IPayUServiceCaller>());
    }

    private static PaymentRequest Order() => new() { PaymentProvider = "PayU", Amount = 12.34m, Currency = "pln", Title = "Order", Email = "buyer@example.com" };

    [Fact]
    public async Task When_PayU_Answers_With_Redirect_Should_Return_Location_Without_Following_It()
    {
        using var server = new RedirectingServer();
        var (services, caller) = Build(server.Url);
        using var _ = services;

        var (orderId, redirectUri, error) = await caller.CreateOrderAsync("token", Order());

        Assert.Equal("ORD-1", orderId);
        Assert.Equal(RedirectTarget, redirectUri);
        Assert.Null(error);
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task When_Several_Orders_Are_Created_Should_Send_Each_Once_Through_The_Factory_Client()
    {
        // Each call disposes its HttpClient; the factory keeps the handler usable for the next order.
        using var server = new RedirectingServer();
        var (services, caller) = Build(server.Url);
        using var _ = services;

        for (var i = 0; i < 3; i++)
        {
            var (orderId, redirectUri, _) = await caller.CreateOrderAsync("token", Order());
            Assert.Equal("ORD-1", orderId);
            Assert.Equal(RedirectTarget, redirectUri);
        }

        Assert.Equal(3, server.Requests);
    }
}

public class RevolutServiceCallerConstructorTests
{
    [Fact]
    public void When_Created_With_The_Original_Two_Argument_Constructor_Should_Verify_Signatures_With_The_System_Clock()
    {
        // The constructor that shipped before the security hardening must keep working for code that
        // creates the caller by hand.
        var caller = new TailoredApps.Shared.Payments.Provider.Revolut.RevolutServiceCaller(
            Options.Create(new TailoredApps.Shared.Payments.Provider.Revolut.RevolutServiceOptions { WebhookSecret = "whsec" }),
            Mock.Of<IHttpClientFactory>());

        Assert.False(caller.VerifyWebhookSignature("{}", "0", "v1=deadbeef"));
    }
}
