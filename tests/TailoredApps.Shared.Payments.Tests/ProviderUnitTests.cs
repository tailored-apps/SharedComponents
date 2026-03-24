using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

// ─── Helpers ─────────────────────────────────────────────────────────────────

file static class Req
{
    public static PaymentRequest Make(string currency = "PLN", decimal amount = 9.99m) =>
        new()
        {
            PaymentProvider = "Test",
            PaymentChannel  = "card",
            PaymentModel    = PaymentModel.OneTime,
            Title           = "Test order",
            Currency        = currency,
            Amount          = amount,
            Email           = "test@example.com",
            FirstName       = "Jan",
            Surname         = "Kowalski",
        };

    public static TransactionStatusChangePayload Webhook(string body, Dictionary<string, StringValues>? qs = null) =>
        new()
        {
            ProviderId      = "test",
            Payload         = body,
            QueryParameters = qs ?? new Dictionary<string, StringValues>(),
        };
}

// ─── Adyen ───────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla AdyenProvider.</summary>
public class AdyenProviderTests
{
    private static AdyenProvider Build(IAdyenServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsAdyen() => Assert.Equal("Adyen", Build(Mock.Of<IAdyenServiceCaller>()).Key);

    [Fact]
    public void Provider_Name_IsAdyen() => Assert.Equal("Adyen", Build(Mock.Of<IAdyenServiceCaller>()).Name);

    [Fact]
    public async Task GetChannels_PLN_NotEmpty()
    {
        var channels = await Build(Mock.Of<IAdyenServiceCaller>()).GetPaymentChannels("PLN");
        Assert.NotEmpty(channels);
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsIdeal()
    {
        var channels = await Build(Mock.Of<IAdyenServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "ideal");
    }

    [Fact]
    public async Task GetChannels_USD_ContainsCard()
    {
        var channels = await Build(Mock.Of<IAdyenServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "scheme");
    }

    [Fact]
    public async Task RequestPayment_CallerSuccess_ReturnsCreated()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.CreateSessionAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(("sess_abc", "https://checkout.adyen.com/pay/abc", null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("sess_abc", result.PaymentUniqueId);
        Assert.Equal("https://checkout.adyen.com/pay/abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_CallerError_ReturnsRejected()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.CreateSessionAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync((null, null, "API error"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Finished()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("psp_123")).ReturnsAsync(PaymentStatusEnum.Finished);
        var result = await Build(mock.Object).GetStatus("psp_123");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Processing()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("psp_123")).ReturnsAsync(PaymentStatusEnum.Processing);
        var result = await Build(mock.Object).GetStatus("psp_123");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_ReturnsStatus()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"AUTHORISATION","success":"true","pspReference":"psp_123"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "valid_hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "valid_hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.NotEqual(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}", new Dictionary<string, StringValues> { { "HmacSignature", "bad" } }));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_SuccessFalse_ReturnsRejected()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"AUTHORISATION","success":"false","pspReference":"psp_123"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "valid_hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "valid_hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_Refund_ReturnsFinished()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"eventCode":"REFUND","success":"true","pspReference":"psp_123"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }
}

// ─── PayU ─────────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla PayUProvider.</summary>
public class PayUProviderTests
{
    private static PayUProvider Build(IPayUServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsPayU() => Assert.Equal("PayU", Build(Mock.Of<IPayUServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_ContainsBlik()
    {
        var channels = await Build(Mock.Of<IPayUServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsCard()
    {
        var channels = await Build(Mock.Of<IPayUServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "c");
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token_abc");
        mock.Setup(m => m.CreateOrderAsync("token_abc", It.IsAny<PaymentRequest>()))
            .ReturnsAsync(("order_123", "https://secure.payu.com/pay/abc", null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("order_123", result.PaymentUniqueId);
        Assert.Equal("https://secure.payu.com/pay/abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_CreateOrderFails_ReturnsRejected()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token");
        mock.Setup(m => m.CreateOrderAsync(It.IsAny<string>(), It.IsAny<PaymentRequest>()))
            .ReturnsAsync((null, null, "Unauthorized"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Completed_ReturnsFinished()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token");
        mock.Setup(m => m.GetOrderStatusAsync("token", "order_1")).ReturnsAsync(PaymentStatusEnum.Finished);
        var result = await Build(mock.Object).GetStatus("order_1");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Pending_ReturnsProcessing()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token");
        mock.Setup(m => m.GetOrderStatusAsync("token", "order_1")).ReturnsAsync(PaymentStatusEnum.Processing);
        var result = await Build(mock.Object).GetStatus("order_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Canceled_ReturnsRejected()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token");
        mock.Setup(m => m.GetOrderStatusAsync("token", "order_1")).ReturnsAsync(PaymentStatusEnum.Rejected);
        var result = await Build(mock.Object).GetStatus("order_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_COMPLETED_ReturnsFinished()
    {
        var body = """{"order":{"status":"COMPLETED","orderId":"order_1"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=test;signature=valid;algorithm=MD5;content=DOCUMENT" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_CANCELED_ReturnsRejected()
    {
        var body = """{"order":{"status":"CANCELED","orderId":"order_1"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=test;signature=valid;algorithm=MD5;content=DOCUMENT" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_PENDING_ReturnsProcessing()
    {
        var body = """{"order":{"status":"PENDING","orderId":"order_1"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, It.IsAny<string>())).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sender=test;signature=valid;algorithm=MD5;content=DOCUMENT" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var qs = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "bad" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ─── Przelewy24 ───────────────────────────────────────────────────────────────

/// <summary>Unit testy dla Przelewy24Provider.</summary>
public class Przelewy24ProviderTests
{
    private static IOptions<Przelewy24ServiceOptions> DefaultOptions() =>
        Options.Create(new Przelewy24ServiceOptions
        {
            MerchantId = 12345,
            ServiceUrl = "https://sandbox.przelewy24.pl",
            NotifyUrl  = "https://example.com/notify",
            ReturnUrl  = "https://example.com/return",
        });

    private static Przelewy24Provider Build(IPrzelewy24ServiceCaller caller) => new(caller, DefaultOptions());

    [Fact]
    public void Provider_Key_IsPrzelewy24() => Assert.Equal("Przelewy24", Build(Mock.Of<IPrzelewy24ServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_NotEmpty()
    {
        var channels = await Build(Mock.Of<IPrzelewy24ServiceCaller>()).GetPaymentChannels("PLN");
        Assert.NotEmpty(channels);
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsCard()
    {
        var channels = await Build(Mock.Of<IPrzelewy24ServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task GetChannels_USD_ContainsCard()
    {
        var channels = await Build(Mock.Of<IPrzelewy24ServiceCaller>()).GetPaymentChannels("USD");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.RegisterTransactionAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync(("token_p24_abc", null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.NotEmpty(result.RedirectUrl!);
        Assert.Contains("token_p24_abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_ApiError_ReturnsRejected()
    {
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.RegisterTransactionAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync((null, "Bad credentials"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_ReturnsProcessing()
    {
        var result = await Build(Mock.Of<IPrzelewy24ServiceCaller>()).GetStatus("sess_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_VerifyOk_ReturnsFinished()
    {
        var body = """{"sessionId":"sess_1","amount":999,"currency":"PLN","orderId":12345}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync("sess_1", 999, "PLN", 12345)).ReturnsAsync(PaymentStatusEnum.Finished);
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_VerifyFails_ReturnsRejected()
    {
        var body = """{"sessionId":"sess_1","amount":999,"currency":"PLN","orderId":12345}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(PaymentStatusEnum.Rejected);
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(It.IsAny<string>())).Returns(false);
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}"));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_MalformedJson_ReturnsRejected()
    {
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(It.IsAny<string>())).Returns(true);
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("not json"));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ─── Tpay ─────────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla TpayProvider.</summary>
public class TpayProviderTests
{
    private static TpayProvider Build(ITpayServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsTpay() => Assert.Equal("Tpay", Build(Mock.Of<ITpayServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_ContainsBlik()
    {
        var channels = await Build(Mock.Of<ITpayServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsCard()
    {
        var channels = await Build(Mock.Of<ITpayServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token_tpay");
        mock.Setup(m => m.CreateTransactionAsync("token_tpay", It.IsAny<PaymentRequest>()))
            .ReturnsAsync(("txn_123", "https://pay.tpay.com/txn/abc"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("txn_123", result.PaymentUniqueId);
        Assert.Equal("https://pay.tpay.com/txn/abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_NoTransactionId_ReturnsRejected()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("token");
        mock.Setup(m => m.CreateTransactionAsync(It.IsAny<string>(), It.IsAny<PaymentRequest>()))
            .ReturnsAsync((null, null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Finished()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.GetAccessTokenAsync()).ReturnsAsync("tok");
        mock.Setup(m => m.GetTransactionStatusAsync("tok", "txn_1")).ReturnsAsync(PaymentStatusEnum.Finished);
        var result = await Build(mock.Object).GetStatus("txn_1");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_paid_ReturnsFinished()
    {
        var body = """{"id":"txn_1","status":"paid","amount":9.99}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "valid_sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "valid_sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_pending_ReturnsProcessing()
    {
        var body = """{"id":"txn_1","status":"pending","amount":9.99}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_error_ReturnsRejected()
    {
        var body = """{"id":"txn_1","status":"error","amount":9.99}""";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var qs = new Dictionary<string, StringValues> { { "X-Signature", "bad" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ─── HotPay ───────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla HotPayProvider.</summary>
public class HotPayProviderTests
{
    private static HotPayProvider Build(IHotPayServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsHotPay() => Assert.Equal("HotPay", Build(Mock.Of<IHotPayServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_NotEmpty()
    {
        var channels = await Build(Mock.Of<IHotPayServiceCaller>()).GetPaymentChannels("PLN");
        Assert.NotEmpty(channels);
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.InitPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync(("pay_abc", "https://hotpay.pl/pay/abc"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("https://hotpay.pl/pay/abc", result.RedirectUrl);
        Assert.NotEmpty(result.PaymentUniqueId!);
    }

    [Fact]
    public async Task RequestPayment_NoUrl_ReturnsRejected()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.InitPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync((null, null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_ReturnsProcessing()
    {
        var result = await Build(Mock.Of<IHotPayServiceCaller>()).GetStatus("pay_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidHash_SUCCESS_ReturnsFinished()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification("valid_hash", "9.99", "pay_1", "SUCCESS")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "HASH",         "valid_hash" },
            { "KWOTA",        "9.99" },
            { "ID_PLATNOSCI", "pay_1" },
            { "STATUS",       "SUCCESS" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("", qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidHash_FAILURE_ReturnsRejected()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification("h", "9.99", "pay_1", "FAILURE")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "HASH",         "h" },
            { "KWOTA",        "9.99" },
            { "ID_PLATNOSCI", "pay_1" },
            { "STATUS",       "FAILURE" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidHash_ReturnsRejected()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var qs = new Dictionary<string, StringValues>
        {
            { "HASH",         "bad" },
            { "KWOTA",        "9.99" },
            { "ID_PLATNOSCI", "pay_1" },
            { "STATUS",       "SUCCESS" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ─── PayNow ───────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla PayNowProvider.</summary>
public class PayNowProviderTests
{
    private static PayNowProvider Build(IPayNowServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsPayNow() => Assert.Equal("PayNow", Build(Mock.Of<IPayNowServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_ContainsBlik()
    {
        var channels = await Build(Mock.Of<IPayNowServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "BLIK");
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.CreatePaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(("pn_abc", "https://api.paynow.pl/checkout/pn_abc"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("pn_abc", result.PaymentUniqueId);
        Assert.Equal("https://api.paynow.pl/checkout/pn_abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_ApiError_ReturnsRejected()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.CreatePaymentAsync(It.IsAny<PaymentRequest>())).ReturnsAsync((null, null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Confirmed_ReturnsFinished()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("pn_1")).ReturnsAsync(PaymentStatusEnum.Finished);
        var result = await Build(mock.Object).GetStatus("pn_1");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Pending_ReturnsProcessing()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.GetPaymentStatusAsync("pn_1")).ReturnsAsync(PaymentStatusEnum.Processing);
        var result = await Build(mock.Object).GetStatus("pn_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_CONFIRMED_ReturnsFinished()
    {
        var body = """{"paymentId":"pn_1","status":"CONFIRMED"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "valid_sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "valid_sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_PENDING_ReturnsProcessing()
    {
        var body = """{"paymentId":"pn_1","status":"PENDING"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_ERROR_ReturnsRejected()
    {
        var body = """{"paymentId":"pn_1","status":"ERROR"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var qs = new Dictionary<string, StringValues> { { "Signature", "sig" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var qs = new Dictionary<string, StringValues> { { "Signature", "bad" } };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}

// ─── Revolut ──────────────────────────────────────────────────────────────────

/// <summary>Unit testy dla RevolutProvider.</summary>
public class RevolutProviderTests
{
    private static RevolutProvider Build(IRevolutServiceCaller caller) => new(caller);

    [Fact]
    public void Provider_Key_IsRevolut() => Assert.Equal("Revolut", Build(Mock.Of<IRevolutServiceCaller>()).Key);

    [Fact]
    public async Task GetChannels_PLN_ContainsRevolutPay()
    {
        var channels = await Build(Mock.Of<IRevolutServiceCaller>()).GetPaymentChannels("PLN");
        Assert.Contains(channels, c => c.Id == "revolut_pay");
    }

    [Fact]
    public async Task GetChannels_EUR_ContainsCard()
    {
        var channels = await Build(Mock.Of<IRevolutServiceCaller>()).GetPaymentChannels("EUR");
        Assert.Contains(channels, c => c.Id == "card");
    }

    [Fact]
    public async Task RequestPayment_Success_ReturnsCreated()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.CreateOrderAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(("rev_abc", "https://checkout.revolut.com/pay/abc"));

        var result = await Build(mock.Object).RequestPayment(Req.Make());

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("rev_abc", result.PaymentUniqueId);
        Assert.Equal("https://checkout.revolut.com/pay/abc", result.RedirectUrl);
    }

    [Fact]
    public async Task RequestPayment_ApiError_ReturnsRejected()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.CreateOrderAsync(It.IsAny<PaymentRequest>())).ReturnsAsync((null, null));

        var result = await Build(mock.Object).RequestPayment(Req.Make());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Completed_ReturnsFinished()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("completed", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Pending_ReturnsProcessing()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("pending", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Cancelled_ReturnsRejected()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("cancelled", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task GetStatus_Unknown_ReturnsFallback()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.GetOrderAsync("rev_1")).ReturnsAsync(("authorised", "rev_1"));
        var result = await Build(mock.Object).GetStatus("rev_1");
        // authorised → Created or Processing depending on implementation
        Assert.True(result.PaymentStatus != PaymentStatusEnum.Finished);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_ORDER_COMPLETED_ReturnsFinished()
    {
        var body = """{"event":"ORDER_COMPLETED","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "1234567890", "v1=valid_sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature",         "v1=valid_sig" },
            { "Revolut-Request-Timestamp", "1234567890" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_ORDER_PAYMENT_DECLINED_ReturnsRejected()
    {
        var body = """{"event":"ORDER_PAYMENT_DECLINED","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature",         "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_ValidSignature_ORDER_AUTHORISED_ReturnsProcessing()
    {
        var body = """{"event":"ORDER_AUTHORISED","order_id":"rev_1"}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "123", "v1=sig")).Returns(true);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature",         "v1=sig" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook(body, qs));
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var qs = new Dictionary<string, StringValues>
        {
            { "Revolut-Signature",         "v1=bad" },
            { "Revolut-Request-Timestamp", "123" },
        };
        var result = await Build(mock.Object).TransactionStatusChange(Req.Webhook("{}", qs));
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }
}
