#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.HotPay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// A cancellation requested by the host must reach the provider, its service caller and the HTTP call.
/// </summary>
public class CancellationTests
{
    private static readonly PaymentRequest Request = new() { PaymentProvider = "HotPay", Amount = 10m, Currency = "PLN", Title = "Order" };

    /// <summary>Answers 200 OK, unless the request has been cancelled - like a real handler does.</summary>
    private sealed class CancellationAwareHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"STATUS":"SUCCESS","ID_PLATNOSCI":"hp_1","PRZEKIERUJ_DO":"https://pay.example/1"}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private static HotPayServiceCaller HotPayCaller(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new HotPayServiceCaller(
            Options.Create(new HotPayServiceOptions { SecretHash = "secret", ServiceUrl = "https://hotpay.example", ReturnUrl = "https://shop.example/return" }),
            factory.Object);
    }

    private static CancellationToken Cancelled()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    [Fact]
    public async Task When_PaymentService_Is_Called_With_A_Token_Should_Pass_It_To_The_Provider()
    {
        using var source = new CancellationTokenSource();
        var provider = new Mock<IPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns("HotPay");
        provider.Setup(p => p.RequestPayment(Request, source.Token)).ReturnsAsync(new PaymentResponse { PaymentUniqueId = "p1" });
        var services = new ServiceCollection().AddSingleton(provider.Object).BuildServiceProvider();

        var response = await new PaymentService(services).RegisterPayment(Request, source.Token);

        Assert.Equal("p1", response.PaymentUniqueId);
        provider.Verify(p => p.RequestPayment(Request, source.Token), Times.Once);
    }

    [Fact]
    public async Task When_A_Provider_Is_Called_With_A_Token_Should_Pass_It_To_Its_Service_Caller()
    {
        using var source = new CancellationTokenSource();
        var caller = new Mock<IHotPayServiceCaller>();
        caller.Setup(c => c.InitPaymentAsync(Request, It.IsAny<string>(), source.Token)).ReturnsAsync(("hp_1", "https://pay.example/1"));

        var response = await new HotPayProvider(caller.Object).RequestPayment(Request, source.Token);

        Assert.Equal("hp_1", response.PaymentUniqueId);
        caller.Verify(c => c.InitPaymentAsync(Request, It.IsAny<string>(), source.Token), Times.Once);
    }

    [Fact]
    public async Task When_The_Token_Is_Cancelled_Should_Abort_The_Http_Call()
    {
        var handler = new CancellationAwareHandler();
        var caller = HotPayCaller(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => caller.InitPaymentAsync(Request, "order-1", Cancelled()));

        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task When_No_Token_Is_Given_Should_Complete_The_Http_Call()
    {
        var handler = new CancellationAwareHandler();
        var caller = HotPayCaller(handler);

        var (paymentId, redirectUrl) = await caller.InitPaymentAsync(Request, "order-1");

        Assert.Equal("hp_1", paymentId);
        Assert.Equal("https://pay.example/1", redirectUrl);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task When_The_Token_Is_Cancelled_Should_Not_Start_A_CashBill_Request()
    {
        // The address is never contacted: cancellation is checked before the request is created.
        var client = new CashbillHttpClient(NullLogger<CashbillHttpClient>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.MakeFormRequest<object>("http://192.0.2.1/payment", "GET", null, Cancelled()));
    }

    [Fact]
    public async Task When_The_Response_Type_Is_Not_A_CashBill_Model_Should_Refuse_Before_Sending()
    {
        // CashBill answers are read with JSON metadata generated at compile time (no reflection, safe for
        // trimming and NativeAOT), which exists for the CashBill models only. The address is never contacted.
        var client = new CashbillHttpClient(NullLogger<CashbillHttpClient>.Instance);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.MakeFormRequest<NotACashBillModel>("http://192.0.2.1/payment", "GET", null, CancellationToken.None));

        Assert.Contains(nameof(NotACashBillModel), exception.Message);
    }

    private sealed class NotACashBillModel
    {
        public string? Id { get; set; }
    }

    [Fact]
    public async Task When_The_Token_Is_Cancelled_Should_Not_List_Providers()
    {
        var services = new ServiceCollection();
        services.AddPayments();
        using var provider = services.BuildServiceProvider();
        var payments = provider.GetRequiredService<IPaymentService>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => payments.GetProviders(Cancelled()));
        Assert.Empty(await payments.GetProviders(CancellationToken.None));
    }
}
