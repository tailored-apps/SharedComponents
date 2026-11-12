using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>The members a payment cannot be started without: provider, amount and currency.</summary>
public class PaymentRequestTests
{
    private static (PaymentService Service, Mock<IPaymentProvider> Provider) Facade()
    {
        var provider = new Mock<IPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns("Tpay");
        provider.Setup(p => p.RequestPayment(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponse { PaymentUniqueId = "pay-1", PaymentStatus = PaymentStatusEnum.Created });
        var services = new ServiceCollection().AddSingleton(provider.Object).BuildServiceProvider();
        return (new PaymentService(services), provider);
    }

    [Fact]
    public void When_Type_Is_Inspected_Should_Require_Exactly_Provider_Amount_And_Currency()
    {
        var required = typeof(PaymentRequest).GetProperties()
            .Where(property => property.IsDefined(typeof(RequiredMemberAttribute), inherit: false))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Amount", "Currency", "PaymentProvider" }, required);
    }

    [Fact]
    public void When_Built_With_The_Constructor_Should_Set_The_Required_Members()
    {
        var request = new PaymentRequest("Tpay", 49.99m, "PLN") { Title = "Order 7" };

        Assert.Equal("Tpay", request.PaymentProvider);
        Assert.Equal(49.99m, request.Amount);
        Assert.Equal("PLN", request.Currency);
        Assert.Equal("Order 7", request.Title);
    }

    [Theory]
    [InlineData("""{"PaymentProvider":"Tpay","Amount":10}""", "Currency")]
    [InlineData("""{"PaymentProvider":"Tpay","Currency":"PLN"}""", "Amount")]
    [InlineData("""{"Amount":10,"Currency":"PLN"}""", "PaymentProvider")]
    public void When_Json_Leaves_Out_A_Required_Member_Should_Refuse_To_Deserialize(string json, string missing)
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentRequest>(json));

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_Json_Has_The_Required_Members_Should_Deserialize()
    {
        var request = JsonSerializer.Deserialize<PaymentRequest>("""{"PaymentProvider":"Tpay","Amount":10.5,"Currency":"PLN","Title":"Order 7"}""");

        Assert.Equal("Tpay", request.PaymentProvider);
        Assert.Equal(10.5m, request.Amount);
        Assert.Equal("PLN", request.Currency);
    }

    [Fact]
    public async Task When_Request_Is_Complete_Should_Pass_It_To_The_Provider()
    {
        // arrange
        var (service, provider) = Facade();
        var request = new PaymentRequest("Tpay", 10m, "PLN");
        using var cancellation = new CancellationTokenSource();

        // act
        var response = await service.RegisterPayment(request, cancellation.Token);

        // assert
        Assert.Equal("pay-1", response.PaymentUniqueId);
        provider.Verify(p => p.RequestPayment(request, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task When_Request_Is_Null_Should_Throw_ArgumentNullException()
    {
        var (service, _) = Facade();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RegisterPayment(null));
    }

    [Theory]
    [InlineData(null, "PLN", "PaymentProvider")]
    [InlineData(" ", "PLN", "PaymentProvider")]
    [InlineData("Tpay", null, "Currency")]
    [InlineData("Tpay", "", "Currency")]
    [InlineData("Tpay", "  ", "Currency")]
    public async Task When_Provider_Or_Currency_Is_Blank_Should_Throw_Before_Calling_The_Provider(string paymentProvider, string currency, string member)
    {
        // arrange
        var (service, provider) = Facade();
        var request = new PaymentRequest(paymentProvider, 10m, currency);

        // act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterPayment(request));

        // assert
        Assert.Contains(member, exception.Message, StringComparison.Ordinal);
        provider.Verify(p => p.RequestPayment(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public async Task When_Amount_Is_Not_Positive_Should_Throw_Before_Calling_The_Provider(double amount)
    {
        // arrange
        var (service, provider) = Facade();
        var request = new PaymentRequest("Tpay", (decimal)amount, "PLN");

        // act
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RegisterPayment(request));

        // assert
        Assert.Equal((decimal)amount, exception.ActualValue);
        provider.Verify(p => p.RequestPayment(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
