#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using global::Stripe;
using global::Stripe.Checkout;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.HotPay;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Provider.Przelewy24;
using TailoredApps.Shared.Payments.Provider.Revolut;
using TailoredApps.Shared.Payments.Provider.Stripe;
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

// ────────────────────────────────────────────────────────────────────────────
// Stripe Provider — unit tests (mocked IStripeServiceCaller)
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla StripeProvider z mockowanym IStripeServiceCaller.</summary>
public class StripeProviderUnitTests
{
    private static StripeProvider Build(IStripeServiceCaller caller) => new(caller);

    private static Session MakeSession(string id, string status, string paymentStatus, string url = "https://checkout.stripe.com/pay/test")
        => new() { Id = id, Status = status, PaymentStatus = paymentStatus, Url = url };

    private static Event MakeEvent(string type, Session? sessionData = null)
        => new()
        {
            Type = type,
            Data = new EventData
            {
                Object = sessionData ?? new Session { Id = "cs_test", Status = "complete", PaymentStatus = "paid" },
            },
        };

    // ─── Properties ────────────────────────────────────────────────────────

    [Fact]
    public void Key_IsStripe() => Assert.Equal("Stripe", Build(Mock.Of<IStripeServiceCaller>()).Key);

    [Fact]
    public void Name_IsStripe() => Assert.Equal("Stripe", Build(Mock.Of<IStripeServiceCaller>()).Name);

    [Fact]
    public void Description_IsNotEmpty() => Assert.NotEmpty(Build(Mock.Of<IStripeServiceCaller>()).Description);

    [Fact]
    public void Url_IsStripeUrl() => Assert.Contains("stripe.com", Build(Mock.Of<IStripeServiceCaller>()).Url);

    // ─── GetPaymentChannels ─────────────────────────────────────────────────

    [Fact]
    public async Task GetChannels_PLN_ContainsBlikP24Card()
    {
        var channels = await Build(Mock.Of<IStripeServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "blik");
        Assert.Contains(channels, c => c.Id == "p24");
        Assert.Contains(channels, c => c.Id == "card");
        Assert.Equal(3, channels.Count);
    }

    [Fact]
    public async Task GetChannels_pln_lowercase_ContainsBlikP24Card()
    {
        var channels = await Build(Mock.Of<IStripeServiceCaller>()).GetPaymentChannels("pln");
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsCardAndSepa()
    {
        var channels = await Build(Mock.Of<IStripeServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "card");
        Assert.Contains(channels, c => c.Id == "sepa_debit");
        Assert.Equal(2, channels.Count);
    }

    [Fact]
    public async Task GetChannels_USD_ContainsOnlyCard()
    {
        var channels = await Build(Mock.Of<IStripeServiceCaller>()).GetPaymentChannels("USD");
        Assert.Single(channels);
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task GetChannels_GBP_FallbackContainsCard()
    {
        var channels = await Build(Mock.Of<IStripeServiceCaller>()).GetPaymentChannels("GBP");
        Assert.Single(channels);
        Assert.Contains(channels, c => c.Id == "card");
    }

    // ─── RequestPayment ─────────────────────────────────────────────────────

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var session = MakeSession("cs_test_123", "open", "unpaid", "https://checkout.stripe.com/pay/cs_test_123");
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(session);

        var result = await Build(mock.Object).RequestPayment(new PaymentRequest
        {
            PaymentProvider = "Stripe",
            PaymentChannel = "card",
            PaymentModel = PaymentModel.OneTime,
            Title = "Test",
            Currency = "PLN",
            Amount = 9.99m,
            Email = "test@example.com",
        });

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("cs_test_123", result.PaymentUniqueId);
        Assert.Equal("https://checkout.stripe.com/pay/cs_test_123", result.RedirectUrl);
    }

    // ─── GetStatus ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_Complete_Paid_ReturnsFinished()
    {
        var session = MakeSession("cs_1", "complete", "paid");
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.GetCheckoutSessionAsync("cs_1")).ReturnsAsync(session);

        var result = await Build(mock.Object).GetStatus("cs_1");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
        Assert.Equal("cs_1", result.PaymentUniqueId);
    }

    [Fact]
    public async Task GetStatus_Complete_Unpaid_ReturnsProcessing()
    {
        var session = MakeSession("cs_1", "complete", "unpaid");
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.GetCheckoutSessionAsync("cs_1")).ReturnsAsync(session);

        var result = await Build(mock.Object).GetStatus("cs_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Expired_ReturnsRejected()
    {
        var session = MakeSession("cs_1", "expired", "unpaid");
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.GetCheckoutSessionAsync("cs_1")).ReturnsAsync(session);

        var result = await Build(mock.Object).GetStatus("cs_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Open_ReturnsCreated()
    {
        var session = MakeSession("cs_1", "open", "unpaid");
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.GetCheckoutSessionAsync("cs_1")).ReturnsAsync(session);

        var result = await Build(mock.Object).GetStatus("cs_1");
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    // ─── TransactionStatusChange ────────────────────────────────────────────

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new StripeException("No signatures found matching the expected signature for payload"));

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{\"type\":\"checkout.session.completed\"}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "t=1,v1=badsig" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
        Assert.Contains("signature", result.ResponseObject?.ToString() ?? "", System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransactionStatusChange_SessionCompleted_Paid_ReturnsFinished()
    {
        var session = MakeSession("cs_1", "complete", "paid");
        var stripeEvent = MakeEvent("checkout.session.completed", session);
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
        Assert.Equal("cs_1", result.PaymentUniqueId);
    }

    [Fact]
    public async Task TransactionStatusChange_SessionCompleted_Unpaid_ReturnsProcessing()
    {
        var session = MakeSession("cs_1", "complete", "no_payment_required");
        var stripeEvent = MakeEvent("checkout.session.completed", session);
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_SessionExpired_ReturnsRejected()
    {
        var stripeEvent = new Event { Type = "checkout.session.expired", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_PaymentIntentSucceeded_ReturnsFinished()
    {
        var stripeEvent = new Event { Type = "payment_intent.succeeded", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_PaymentIntentFailed_ReturnsRejected()
    {
        var stripeEvent = new Event { Type = "payment_intent.payment_failed", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownEvent_ReturnsProcessing()
    {
        var stripeEvent = new Event { Type = "payment_method.attached", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_EmptySignature_FallsToInvalidSig()
    {
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), string.Empty))
            .Throws(new StripeException("No signatures found matching the expected signature for payload"));

        var payload = new TransactionStatusChangePayload
        {
            Payload = "{}",
            QueryParameters = new Dictionary<string, StringValues>(), // No Stripe-Signature key
        };

        var result = await Build(mock.Object).TransactionStatusChange(payload);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ─── HandleWebhookAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new StripeException("Webhook signature verification failed: No signatures found"));

        var request = new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "t=1,v1=bad" } },
        };

        var result = await ((IWebhookPaymentProvider)Build(mock.Object)).HandleWebhookAsync(request);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_SessionCompleted_Paid_ReturnsOk_Finished()
    {
        var session = MakeSession("cs_1", "complete", "paid", "https://stripe.com/pay");
        var stripeEvent = MakeEvent("checkout.session.completed", session);

        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var request = new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid_sig" } },
        };

        var result = await ((IWebhookPaymentProvider)Build(mock.Object)).HandleWebhookAsync(request);
        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_UnknownEvent_ReturnsIgnore()
    {
        var stripeEvent = new Event { Type = "customer.created", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var request = new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid_sig" } },
        };

        var result = await ((IWebhookPaymentProvider)Build(mock.Object)).HandleWebhookAsync(request);
        Assert.True(result.Ignored);
    }

    [Fact]
    public async Task HandleWebhook_SessionExpired_ReturnsIgnore()
    {
        var stripeEvent = new Event { Type = "checkout.session.expired", Data = new EventData { Object = new Session() } };
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var request = new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        // expired → Rejected + "OK" (no "signature") → then check IsNullOrEmpty(paymentUniqueId) → Ignore
        var result = await ((IWebhookPaymentProvider)Build(mock.Object)).HandleWebhookAsync(request);
        Assert.True(result.Success || result.Ignored); // either Ignore or Ok, not Fail
    }

    [Fact]
    public async Task HandleWebhook_SessionCompleted_Unpaid_ReturnsIgnore()
    {
        var session = MakeSession("cs_1", "complete", "unpaid");
        var stripeEvent = MakeEvent("checkout.session.completed", session);
        var mock = new Mock<IStripeServiceCaller>();
        mock.Setup(m => m.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(stripeEvent);

        var request = new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "valid" } },
        };

        var result = await ((IWebhookPaymentProvider)Build(mock.Object)).HandleWebhookAsync(request);
        // Processing → Ignore
        Assert.True(result.Ignored);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Tpay — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class TpayProviderAdditionalTests
{
    private static TpayProvider Build(ITpayServiceCaller caller) => new(caller);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<ITpayServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsTpay() => Assert.Contains("tpay", Build(Mock.Of<ITpayServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_USD_ContainsBlik()
    {
        var channels = await Build(Mock.Of<ITpayServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task GetStatus_Processing()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("tok");
        mock.Setup(m => m.GetTransactionStatusAsync("tok", "txn_1")).ReturnsAsync(PaymentStatusEnum.Processing);
        var result = await Build(mock.Object).GetStatus("txn_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Rejected()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("tok");
        mock.Setup(m => m.GetTransactionStatusAsync("tok", "txn_1")).ReturnsAsync(PaymentStatusEnum.Rejected);
        var result = await Build(mock.Object).GetStatus("txn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_correct_Status_ReturnsFinished()
    {
        var body = """{"id":"txn_1","status":"correct"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_chargeback_Status_ReturnsRejected()
    {
        var body = """{"id":"txn_1","status":"chargeback"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_TRUE_LegacyStatus_ReturnsFinished()
    {
        var body = """{"id":"txn_1","status":"TRUE"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_FALSE_LegacyStatus_ReturnsRejected()
    {
        var body = """{"id":"txn_1","status":"FALSE"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_TrStatus_Paid_ReturnsFinished()
    {
        var body = """{"id":"txn_1","tr_status":"paid"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownStatus_ReturnsProcessing()
    {
        var body = """{"id":"txn_1","status":"unknown_state"}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_StillReturns()
    {
        var body = "NOT_JSON";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        // JSON parse fails → catch → status = Processing
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// PayU — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class PayUProviderAdditionalTests
{
    private static PayUProvider Build(IPayUServiceCaller caller) => new(caller);

    [Fact]
    public void Name_IsPayU() => Assert.Equal("PayU", Build(Mock.Of<IPayUServiceCaller>()).Name);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IPayUServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsPayu() => Assert.Contains("payu", Build(Mock.Of<IPayUServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_USD_ContainsCard()
    {
        var channels = await Build(Mock.Of<IPayUServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "c");
    }

    [Fact]
    public async Task TransactionStatusChange_REJECTED_Status_ReturnsRejected()
    {
        var body = """{"order":{"status":"REJECTED","orderId":"order_1"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=x;signature=valid;algorithm=MD5" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_ReturnsProcessing()
    {
        var body = "NOT_JSON";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=x;signature=valid;algorithm=MD5" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_NoOrderProperty_ReturnsProcessing()
    {
        var body = """{"status":"COMPLETED"}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=x;signature=valid;algorithm=MD5" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task RequestPayment_TokenFetchFails_ThrowsOrRejected()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync(string.Empty);
        mock.Setup(m => m.CreateOrderAsync(string.Empty, It.IsAny<PaymentRequest>()))
            .ReturnsAsync((null, null, "Error"));
        var result = await Build(mock.Object).RequestPayment(new PaymentRequest
        {
            PaymentProvider = "PayU",
            Currency = "PLN",
            Amount = 1m,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Przelewy24 — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class Przelewy24ProviderAdditionalTests
{
    private static Przelewy24Provider Build(IPrzelewy24ServiceCaller caller, string serviceUrl = "https://secure.przelewy24.pl")
        => new(caller, Options.Create(new Przelewy24ServiceOptions
        {
            MerchantId = 12345,
            PosId = 12345,
            ServiceUrl = serviceUrl,
            NotifyUrl = "https://example.com/notify",
            ReturnUrl = "https://example.com/return",
        }));

    [Fact]
    public void Name_IsPrzelewy24() => Assert.Equal("Przelewy24", Build(Mock.Of<IPrzelewy24ServiceCaller>()).Name);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IPrzelewy24ServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsPrzelewy24() => Assert.Contains("przelewy24", Build(Mock.Of<IPrzelewy24ServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_USD_ContainsCard()
    {
        var channels = await Build(Mock.Of<IPrzelewy24ServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task RequestPayment_RedirectUrl_ContainsServiceUrl()
    {
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.RegisterTransactionAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync(("token_abc", null));
        var result = await Build(mock.Object, "https://sandbox.przelewy24.pl").RequestPayment(new PaymentRequest
        {
            Currency = "PLN",
            Amount = 10m,
            Email = "x@example.com",
        });
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Contains("sandbox.przelewy24.pl", result.RedirectUrl);
        Assert.Contains("token_abc", result.RedirectUrl);
    }

    [Fact]
    public async Task TransactionStatusChange_MissingFields_ReturnsRejected()
    {
        var body = """{"sessionId":"s1","currency":"PLN"}"""; // missing amount and orderId
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>(),
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_Processing_VerifyReturnsProcessing()
    {
        var body = """{"sessionId":"sess_1","amount":999,"currency":"PLN","orderId":12345}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync("sess_1", 999, "PLN", 12345)).ReturnsAsync(PaymentStatusEnum.Processing);
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>(),
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// PayNow — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class PayNowProviderAdditionalTests
{
    private static PayNowProvider Build(IPayNowServiceCaller caller) => new(caller);

    [Fact]
    public void Name_IsPayNow() => Assert.Equal("PayNow", Build(Mock.Of<IPayNowServiceCaller>()).Name);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IPayNowServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsPaynow() => Assert.Contains("paynow", Build(Mock.Of<IPayNowServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_EUR_ContainsBlik()
    {
        var channels = await Build(Mock.Of<IPayNowServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "BLIK");
    }

    [Fact]
    public async Task GetStatus_Rejected()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("pn_1")).ReturnsAsync(PaymentStatusEnum.Rejected);
        var result = await Build(mock.Object).GetStatus("pn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Created()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("pn_1")).ReturnsAsync(PaymentStatusEnum.Created);
        var result = await Build(mock.Object).GetStatus("pn_1");
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_REJECTED_Status_ReturnsRejected()
    {
        var body = """{"paymentId":"pn_1","status":"REJECTED"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ABANDONED_Status_ReturnsRejected()
    {
        var body = """{"paymentId":"pn_1","status":"ABANDONED"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_ReturnsProcessing()
    {
        var body = "NOT_JSON";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownStatus_ReturnsProcessing()
    {
        var body = """{"paymentId":"pn_1","status":"UNKNOWN"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Adyen — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class AdyenProviderAdditionalTests
{
    private static AdyenProvider Build(IAdyenServiceCaller caller) => new(caller);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IAdyenServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsAdyen() => Assert.Contains("adyen", Build(Mock.Of<IAdyenServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_PLN_ContainsOnlineBanking()
    {
        var channels = await Build(Mock.Of<IAdyenServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "onlineBanking_PL");
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsSepa()
    {
        var channels = await Build(Mock.Of<IAdyenServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "sepadirectdebit");
    }

    [Fact]
    public async Task GetStatus_Rejected()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("psp_1")).ReturnsAsync(PaymentStatusEnum.Rejected);
        var result = await Build(mock.Object).GetStatus("psp_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Created()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("psp_1")).ReturnsAsync(PaymentStatusEnum.Created);
        var result = await Build(mock.Object).GetStatus("psp_1");
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_CANCELLATION_ReturnsRejected()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"CANCELLATION","success":"true","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_AUTHORISATION_FAILED_ReturnsRejected()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"AUTHORISATION_FAILED","success":"true","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownEvent_ReturnsProcessing()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"UNKNOWN_EVT","success":"true","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_ReturnsProcessing()
    {
        var body = "NOT_JSON";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_DirectObject_NotWrapped_ReturnsFinished()
    {
        // Body without notificationItems wrapper — eventCode directly at root
        var body = """{"eventCode":"AUTHORISATION","success":"true","pspReference":"psp_1"}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_REFUND_SuccessFalse_ReturnsRejected()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"REFUND","success":"false","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Revolut — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class RevolutProviderAdditionalTests
{
    private static RevolutProvider Build(IRevolutServiceCaller caller) => new(caller);

    [Fact]
    public void Name_IsRevolut() => Assert.Equal("Revolut", Build(Mock.Of<IRevolutServiceCaller>()).Name);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IRevolutServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsRevolut() => Assert.Contains("revolut", Build(Mock.Of<IRevolutServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_USD_ContainsCard()
    {
        var channels = await Build(Mock.Of<IRevolutServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task GetStatus_Failed_ReturnsRejected()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("failed", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Processing_State_ReturnsProcessing()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("processing", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Unknown_ReturnsCreated()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("initiated", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ORDER_CANCELLED_ReturnsRejected()
    {
        var body = """{"event":"ORDER_CANCELLED","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature", "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_PAYMENT_DECLINED_ReturnsRejected()
    {
        var body = """{"event":"PAYMENT_DECLINED","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature", "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownEvent_ReturnsProcessing()
    {
        var body = """{"event":"ORDER_PROCESSING","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature", "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_ReturnsProcessing()
    {
        var body = "NOT_JSON";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature", "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            Payload = body,
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// HotPay — dodatkowe testy pokrycia
// ────────────────────────────────────────────────────────────────────────────

public class HotPayProviderAdditionalTests
{
    private static HotPayProvider Build(IHotPayServiceCaller caller) => new(caller);

    [Fact]
    public void Name_IsHotPay() => Assert.Equal("HotPay", Build(Mock.Of<IHotPayServiceCaller>()).Name);

    [Fact]
    public void Description_NotEmpty() => Assert.NotEmpty(Build(Mock.Of<IHotPayServiceCaller>()).Description);

    [Fact]
    public void Url_ContainsHotpay() => Assert.Contains("hotpay", Build(Mock.Of<IHotPayServiceCaller>()).Url);

    [Fact]
    public async Task GetChannels_EUR_ContainsCard()
    {
        var channels = await Build(Mock.Of<IHotPayServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task RequestPayment_OnlyIdReturned_StillCreated()
    {
        // resultId non-null, redirectUrl null → status Created
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.InitPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync(("pay_abc", null));
        var result = await Build(mock.Object).RequestPayment(new PaymentRequest
        {
            PaymentProvider = "HotPay",
            Currency = "PLN",
            Amount = 9.99m,
        });
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_HasPaymentId()
    {
        var result = await Build(Mock.Of<IHotPayServiceCaller>()).GetStatus("pay_xyz");
        Assert.Equal("pay_xyz", result.PaymentUniqueId);
    }

    [Fact]
    public async Task TransactionStatusChange_UnknownStatus_ReturnsRejected()
    {
        // Any status other than SUCCESS is mapped as Rejected
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification("h", "9.99", "pay_1", "PENDING")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "HASH", "h" }, { "KWOTA", "9.99" }, { "ID_PLATNOSCI", "pay_1" }, { "STATUS", "PENDING" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(new TransactionStatusChangePayload
        {
            QueryParameters = qs,
        });
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}
