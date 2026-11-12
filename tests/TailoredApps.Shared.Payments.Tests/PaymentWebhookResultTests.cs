#nullable enable
using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Stripe;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using TailoredApps.Shared.Payments.Provider.HotPay;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Provider.Przelewy24;
using TailoredApps.Shared.Payments.Provider.Revolut;
using TailoredApps.Shared.Payments.Provider.Stripe;
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

public class PaymentWebhookResultTests
{
    private static readonly PaymentResponse Finished = new() { PaymentUniqueId = "p1", PaymentStatus = PaymentStatusEnum.Finished };

    [Fact]
    public void When_Created_With_Ok_Should_Be_Processed_And_Carry_The_Payment()
    {
        var result = PaymentWebhookResult.Ok(Finished);

        var processed = Assert.IsType<PaymentWebhookProcessed>(result);
        Assert.Same(Finished, processed.Payment);
        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Same(Finished, result.PaymentResponse);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void When_Created_With_Ignore_Should_Be_Ignored_And_Still_Count_As_Success()
    {
        var result = PaymentWebhookResult.Ignore("not tracked");

        var ignored = Assert.IsType<PaymentWebhookIgnored>(result);
        Assert.Equal("not tracked", ignored.Reason);
        Assert.Null(ignored.Payment);
        Assert.True(result.Success);
        Assert.True(result.Ignored);
        Assert.Null(result.PaymentResponse);
        Assert.Equal("not tracked", result.ErrorMessage);
    }

    [Fact]
    public void When_Created_With_Fail_Should_Be_Rejected_Without_A_Specific_Reason()
    {
        var result = PaymentWebhookResult.Fail("boom");

        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(PaymentWebhookRejection.Unspecified, rejected.Rejection);
        Assert.False(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public void When_Created_With_Reject_Should_Carry_The_Reason()
    {
        var result = PaymentWebhookResult.Reject(PaymentWebhookRejection.InvalidSignature, "bad signature");

        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(PaymentWebhookRejection.InvalidSignature, rejected.Rejection);
        Assert.Equal("bad signature", rejected.Message);
    }

    [Theory]
    [InlineData(PaymentStatusEnum.Finished, false)]
    [InlineData(PaymentStatusEnum.Rejected, false)]
    [InlineData(PaymentStatusEnum.Created, false)]
    [InlineData(PaymentStatusEnum.Processing, true)]
    public void When_Classifying_A_Parsed_Payment_Should_Ignore_Only_Payments_Still_In_Progress(PaymentStatusEnum status, bool ignored)
    {
        var payment = new PaymentResponse { PaymentUniqueId = "p1", PaymentStatus = status };

        var result = PaymentWebhookResult.FromStatus(payment);

        Assert.Equal(ignored, result is PaymentWebhookIgnored);
        Assert.Equal(!ignored, result is PaymentWebhookProcessed);
        Assert.Same(payment, result.ToPaymentResponse());
    }

    [Fact]
    public void When_A_Rejection_Is_Converted_To_A_Payment_Response_Should_Be_Rejected_With_The_Message()
    {
        var response = PaymentWebhookResult.Reject(PaymentWebhookRejection.MalformedPayload, "Missing fields").ToPaymentResponse();

        Assert.Equal(PaymentStatusEnum.Rejected, response.PaymentStatus);
        Assert.Equal("Missing fields", response.ResponseObject);
        Assert.Null(response.PaymentUniqueId);
    }

    [Fact]
    public void When_An_Ignored_Result_Without_Payment_Is_Converted_Should_Report_A_Payment_In_Progress()
    {
        var response = PaymentWebhookResult.Ignore("nothing to do").ToPaymentResponse();

        Assert.Equal(PaymentStatusEnum.Processing, response.PaymentStatus);
    }

    [Fact]
    public void When_Switching_Over_The_Three_Cases_Should_Need_No_Default_Branch()
    {
        // The hierarchy is closed, so these three cases are all there is.
        static string Describe(PaymentWebhookResult result) => result switch
        {
            PaymentWebhookProcessed processed => "processed:" + processed.Payment.PaymentStatus,
            PaymentWebhookIgnored => "ignored",
            PaymentWebhookRejected rejected => "rejected:" + rejected.Rejection,
        };

        Assert.Equal("processed:Finished", Describe(PaymentWebhookResult.Ok(Finished)));
        Assert.Equal("ignored", Describe(PaymentWebhookResult.Ignore()));
        Assert.Equal("rejected:NotConfigured", Describe(PaymentWebhookResult.Reject(PaymentWebhookRejection.NotConfigured, "x")));
    }
}

/// <summary>
/// Providers must say <em>why</em> a webhook was rejected through <see cref="PaymentWebhookRejected.Rejection"/>,
/// not through the wording of a message.
/// </summary>
public class ProviderWebhookRejectionTests
{
    private static PaymentWebhookRequest Body(string body) => new() { Body = body };

    private static PaymentWebhookRejected AssertRejected(PaymentWebhookResult result, PaymentWebhookRejection expected)
    {
        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(expected, rejected.Rejection);
        Assert.False(result.Success);
        return rejected;
    }

    // A loose mock answers every signature check with false, i.e. "the signature is not valid".

    [Fact]
    public async Task When_Adyen_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new AdyenProvider(Mock.Of<IAdyenServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_HotPay_Hash_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new HotPayProvider(Mock.Of<IHotPayServiceCaller>()).HandleWebhookAsync(new PaymentWebhookRequest()), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_PayNow_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new PayNowProvider(Mock.Of<IPayNowServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_PayU_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new PayUProvider(Mock.Of<IPayUServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_Revolut_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new RevolutProvider(Mock.Of<IRevolutServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_Tpay_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new TpayProvider(Mock.Of<ITpayServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_Stripe_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
    {
        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Throws(new StripeException("no match"));

        var result = await new StripeProvider(caller.Object).HandleWebhookAsync(Body("{}"));

        AssertRejected(result, PaymentWebhookRejection.InvalidSignature);
    }

    [Fact]
    public async Task When_A_Valid_Notification_Reports_A_Payment_In_Progress_Should_Ignore_It_And_Keep_What_It_Reported()
    {
        const string body = """{"paymentId":"pn_7","status":"PENDING"}""";
        var caller = new Mock<IPayNowServiceCaller>();
        caller.Setup(c => c.VerifySignature(body, "sig")).Returns(true);
        var request = new PaymentWebhookRequest { Body = body, Headers = new Dictionary<string, StringValues> { { "Signature", "sig" } } };

        var result = await new PayNowProvider(caller.Object).HandleWebhookAsync(request);

        var ignored = Assert.IsType<PaymentWebhookIgnored>(result);
        Assert.Equal("pn_7", ignored.Payment?.PaymentUniqueId);
        Assert.Null(result.PaymentResponse);
    }

    // ── Przelewy24: distinguishes a forged, an unreadable and an unconfirmable notification

    private static Przelewy24Provider Przelewy24(Mock<IPrzelewy24ServiceCaller> caller)
        => new(caller.Object, Options.Create(new Przelewy24ServiceOptions()));

    [Fact]
    public async Task When_Przelewy24_Signature_Is_Invalid_Should_Reject_As_InvalidSignature()
        => AssertRejected(await Przelewy24(new Mock<IPrzelewy24ServiceCaller>()).HandleWebhookAsync(Body("{}")), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_Przelewy24_Notification_Lacks_Required_Fields_Should_Reject_As_MalformedPayload()
    {
        const string body = """{"sessionId":"s1"}""";
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(body)).Returns(true);
        var provider = Przelewy24(caller);

        var rejected = AssertRejected(await provider.HandleWebhookAsync(Body(body)), PaymentWebhookRejection.MalformedPayload);

        Assert.Equal("Missing fields", rejected.Message);
        // The older entry point reports the same outcome in its own shape.
        var legacy = await provider.TransactionStatusChange(new TransactionStatusChangePayload { Payload = body, QueryParameters = new() });
        Assert.Equal(PaymentStatusEnum.Rejected, legacy.PaymentStatus);
        Assert.Equal("Missing fields", legacy.ResponseObject);
    }

    [Fact]
    public async Task When_Przelewy24_Cannot_Confirm_The_Transaction_Should_Reject_As_ProcessingFailed()
    {
        const string body = """{"sessionId":"s1","amount":100,"currency":"PLN","orderId":7}""";
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(body)).Returns(true);
        caller.Setup(c => c.VerifyTransactionAsync("s1", 100, "PLN", 7, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("gateway down"));

        var result = await Przelewy24(caller).HandleWebhookAsync(Body(body));

        AssertRejected(result, PaymentWebhookRejection.ProcessingFailed);
    }

    // ── CashBill

    private static PaymentWebhookRequest CashBillRequest(string? args, string? sign)
    {
        var query = new Dictionary<string, StringValues> { { "cmd", "transactionStatusChanged" } };
        if (args is not null) query["args"] = args;
        if (sign is not null) query["sign"] = sign;
        return new PaymentWebhookRequest { Query = query };
    }

    [Fact]
    public async Task When_CashBill_Transaction_Id_Is_Missing_Should_Reject_As_MalformedPayload()
        => AssertRejected(await new CashBillProvider(Mock.Of<ICashbillServiceCaller>()).HandleWebhookAsync(CashBillRequest(null, "abc")), PaymentWebhookRejection.MalformedPayload);

    [Fact]
    public async Task When_CashBill_Signature_Is_Missing_Should_Reject_As_InvalidSignature()
        => AssertRejected(await new CashBillProvider(Mock.Of<ICashbillServiceCaller>()).HandleWebhookAsync(CashBillRequest("TX-1", null)), PaymentWebhookRejection.InvalidSignature);

    [Fact]
    public async Task When_CashBill_Signature_Does_Not_Match_Should_Reject_As_InvalidSignature()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>(), It.IsAny<CancellationToken>())).ReturnsAsync("0123456789abcdef0123456789abcdef");

        var result = await new CashBillProvider(caller.Object).HandleWebhookAsync(CashBillRequest("TX-1", "ffffffffffffffffffffffffffffffff"));

        AssertRejected(result, PaymentWebhookRejection.InvalidSignature);
    }

    [Fact]
    public async Task When_CashBill_Secret_Is_Not_Configured_Should_Reject_As_NotConfigured()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no secret"));

        var result = await new CashBillProvider(caller.Object).HandleWebhookAsync(CashBillRequest("TX-1", "abc"));

        AssertRejected(result, PaymentWebhookRejection.NotConfigured);
    }
}

public class PaymentServiceWebhookRejectionTests
{
    private static PaymentService ServiceWith(params IPaymentProvider[] providers)
    {
        var services = new ServiceCollection();
        foreach (var provider in providers)
        {
            services.AddSingleton(provider);
        }

        return new PaymentService(services.BuildServiceProvider());
    }

    [Fact]
    public async Task When_Provider_Key_Is_Not_Registered_Should_Reject_As_UnknownProvider()
    {
        var result = await ServiceWith().HandleWebhookAsync("Nope", new PaymentWebhookRequest());

        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(PaymentWebhookRejection.UnknownProvider, rejected.Rejection);
    }

    [Fact]
    public async Task When_Provider_Does_Not_Handle_Webhooks_Should_Reject_As_NotSupported()
    {
        var provider = new Mock<IPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns("Plain");

        var result = await ServiceWith(provider.Object).HandleWebhookAsync("Plain", new PaymentWebhookRequest());

        var rejected = Assert.IsType<PaymentWebhookRejected>(result);
        Assert.Equal(PaymentWebhookRejection.NotSupported, rejected.Rejection);
    }
}
