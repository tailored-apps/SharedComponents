using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Stripe;
using Stripe.Checkout;
using TailoredApps.Shared.Payments.Provider.Stripe;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Stripe reports every error through StripeException; the provider turns them into the outcomes every other
/// provider reports: PaymentGatewayException when Stripe could not handle the call, Rejected when it refused it.
/// </summary>
public class StripeGatewayErrorTests
{
    private static PaymentRequest Request() => new("Stripe", 10m, "PLN") { Title = "Order 1", Email = "anna@demo.example" };

    private static StripeException Answer(HttpStatusCode status, string message = "Stripe said no.")
        => new(status, new StripeError { Message = message }, message)
        {
            StripeResponse = new StripeResponse(status, null, $$$"""{"error":{"message":"{{{message}}}"}}"""),
        };

    private static (StripeProvider Provider, Mock<IStripeServiceCaller> Caller) Create()
    {
        var caller = new Mock<IStripeServiceCaller>();
        return (new StripeProvider(caller.Object), caller);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task When_Stripe_Cannot_Handle_The_Payment_Should_Throw_PaymentGatewayException(HttpStatusCode status)
    {
        var (provider, caller) = Create();
        var stripeError = Answer(status);
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(stripeError);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.RequestPayment(Request()));

        Assert.Equal("Stripe", exception.Provider);
        Assert.Equal(status, exception.StatusCode);
        Assert.True(exception.IsTransient);
        Assert.Same(stripeError, exception.InnerException);
        Assert.Contains("Stripe said no.", exception.ResponseBody);
    }

    [Fact]
    public async Task When_The_Stripe_Exception_Has_No_Http_Status_Should_Treat_It_As_No_Answer()
    {
        var (provider, caller) = Create();
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("The connection was closed."));

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.RequestPayment(Request()));

        Assert.IsType<StripeException>(exception.InnerException);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task When_Stripe_Refuses_The_Payment_Should_Return_Rejected_With_Its_Message(HttpStatusCode status)
    {
        var (provider, caller) = Create();
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(Answer(status, "The payment method type \"blik\" is invalid."));

        var response = await provider.RequestPayment(Request());

        Assert.Equal(PaymentStatusEnum.Rejected, response.PaymentStatus);
        Assert.Equal("The payment method type \"blik\" is invalid.", response.ResponseObject);
        Assert.Null(response.PaymentUniqueId);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]       // even an unknown session says nothing about a payment
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task When_The_Status_Query_Fails_Should_Throw_PaymentGatewayException(HttpStatusCode status)
    {
        var (provider, caller) = Create();
        caller.Setup(c => c.GetCheckoutSessionAsync("cs_test_1", It.IsAny<CancellationToken>())).ThrowsAsync(Answer(status));

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.GetStatus("cs_test_1"));

        Assert.Equal(status, exception.StatusCode);
        Assert.IsType<StripeException>(exception.InnerException);
    }

    [Fact]
    public async Task When_Stripe_Creates_The_Session_Should_Return_Its_Id_And_Page()
    {
        var (provider, caller) = Create();
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { Id = "cs_test_1", Url = "https://checkout.stripe.com/c/pay/cs_test_1" });

        var response = await provider.RequestPayment(Request());

        Assert.Equal(PaymentStatusEnum.Created, response.PaymentStatus);
        Assert.Equal("cs_test_1", response.PaymentUniqueId);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_1", response.RedirectUrl);
    }

    [Fact]
    public async Task When_Stripe_Is_Down_Should_Reach_The_Caller_Of_The_Payment_Service_As_HttpRequestException()
    {
        var (provider, caller) = Create();
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(Answer(HttpStatusCode.ServiceUnavailable));

        // The one catch an application needs for every provider, Stripe included.
        await Assert.ThrowsAnyAsync<System.Net.Http.HttpRequestException>(() => provider.RequestPayment(Request()));
    }
}
