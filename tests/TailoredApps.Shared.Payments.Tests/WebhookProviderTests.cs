using System.Collections.Generic;
using System.Threading;
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

// ─── Adyen ───────────────────────────────────────────────────────────────────

public class AdyenWebhookTests
{
    private static AdyenProvider Build(IAdyenServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidHmac_AUTHORISATION_success_ReturnsOk_Finished()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":"hmac_val"},"eventCode":"AUTHORISATION","success":"true","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(It.IsAny<string>(), "hmac_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>(),
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidHmac_AUTHORISATION_failed_ReturnsOk_Rejected()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":"hmac_val"},"eventCode":"AUTHORISATION","success":"false","pspReference":"psp_1"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(It.IsAny<string>(), "hmac_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>(),
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidHmac_ReturnsFail()
    {
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues>(),
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_ValidHmac_UnknownEvent_ReturnsIgnore()
    {
        var body = """{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":"hmac_val"},"eventCode":"UNKNOWN_EVENT","success":"true"}}]}""";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(It.IsAny<string>(), "hmac_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>(),
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Ignored);
    }
}

// ─── HotPay ──────────────────────────────────────────────────────────────────

public class HotPayWebhookTests
{
    private static HotPayProvider Build(IHotPayServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidHash_SUCCESS_ReturnsOk_Finished()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification("abc123", "99.99", "PAY_001", "SUCCESS")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Query = new Dictionary<string, StringValues>
            {
                { "HASH",         "abc123"  },
                { "KWOTA",        "99.99"   },
                { "ID_PLATNOSCI", "PAY_001" },
                { "STATUS",       "SUCCESS" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
        Assert.Equal("PAY_001", result.PaymentResponse!.PaymentUniqueId);
    }

    [Fact]
    public async Task HandleWebhook_ValidHash_FAILURE_ReturnsOk_Rejected()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification("abc123", "99.99", "PAY_001", "FAILURE")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Query = new Dictionary<string, StringValues>
            {
                { "HASH",         "abc123"  },
                { "KWOTA",        "99.99"   },
                { "ID_PLATNOSCI", "PAY_001" },
                { "STATUS",       "FAILURE" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidHash_ReturnsFail()
    {
        var mock = new Mock<IHotPayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var request = new PaymentWebhookRequest
        {
            Query = new Dictionary<string, StringValues>
            {
                { "HASH",         "bad"     },
                { "KWOTA",        "99.99"   },
                { "ID_PLATNOSCI", "PAY_001" },
                { "STATUS",       "SUCCESS" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}

// ─── PayNow ──────────────────────────────────────────────────────────────────

public class PayNowWebhookTests
{
    private static PayNowProvider Build(IPayNowServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidSignature_CONFIRMED_ReturnsOk_Finished()
    {
        var body = """{"paymentId":"pn_1","status":"CONFIRMED"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_REJECTED_ReturnsOk_Rejected()
    {
        var body = """{"paymentId":"pn_1","status":"REJECTED"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = """{"status":"CONFIRMED"}""",
            Headers = new Dictionary<string, StringValues> { { "Signature", "bad" } },
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_PENDING_ReturnsIgnore()
    {
        var body = """{"paymentId":"pn_1","status":"PENDING"}""";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Ignored);
    }
}

// ─── PayU ────────────────────────────────────────────────────────────────────

public class PayUWebhookTests
{
    private static PayUProvider Build(IPayUServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidSignature_COMPLETED_ReturnsOk_Finished()
    {
        var body = """{"order":{"orderId":"ord_1","status":"COMPLETED"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_CANCELED_ReturnsOk_Rejected()
    {
        var body = """{"order":{"orderId":"ord_1","status":"CANCELED"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = """{"order":{"status":"COMPLETED"}}""",
            Headers = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "bad" } },
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_PENDING_ReturnsIgnore()
    {
        var body = """{"order":{"orderId":"ord_1","status":"PENDING"}}""";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig_val")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "OpenPayU-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Ignored);
    }
}

// ─── Przelewy24 ──────────────────────────────────────────────────────────────

public class Przelewy24WebhookTests
{
    private static Przelewy24Provider Build(IPrzelewy24ServiceCaller caller)
        => new(caller, Options.Create(new Przelewy24ServiceOptions()));

    [Fact]
    public async Task HandleWebhook_ValidSignature_VerifyOk_ReturnsOk_Finished()
    {
        var body = """{"sessionId":"sess_1","orderId":12345,"merchantId":100,"amount":1000,"currency":"PLN","sign":"abc"}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync("sess_1", 1000, "PLN", 12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentStatusEnum.Finished);

        var request = new PaymentWebhookRequest { Body = body };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_VerifyFails_ReturnsOk_Rejected()
    {
        var body = """{"sessionId":"sess_1","orderId":12345,"merchantId":100,"amount":1000,"currency":"PLN","sign":"abc"}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync("sess_1", 1000, "PLN", 12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentStatusEnum.Rejected);

        var request = new PaymentWebhookRequest { Body = body };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var body = """{"sessionId":"sess_1","orderId":12345,"merchantId":100,"amount":1000,"currency":"PLN","sign":"bad"}""";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}

// ─── Revolut ─────────────────────────────────────────────────────────────────

public class RevolutWebhookTests
{
    private static RevolutProvider Build(IRevolutServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidSignature_ORDER_COMPLETED_ReturnsOk_Finished()
    {
        var body = """{"event":"ORDER_COMPLETED","order":{"id":"rev_1","state":"completed"}}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "ts_123", "v1=hexhex")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", "ts_123"    },
                { "Revolut-Signature",         "v1=hexhex" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_ORDER_CANCELLED_ReturnsOk_Rejected()
    {
        var body = """{"event":"ORDER_CANCELLED","order":{"id":"rev_1"}}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "ts_123", "v1=hexhex")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", "ts_123"    },
                { "Revolut-Signature",         "v1=hexhex" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = """{"event":"ORDER_COMPLETED"}""",
            Headers = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", "ts_123" },
                { "Revolut-Signature",         "bad"    },
            },
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_UnknownEvent_ReturnsIgnore()
    {
        var body = """{"event":"ORDER_AUTHORISED","order":{"id":"rev_1"}}""";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "ts_123", "v1=hexhex")).Returns(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", "ts_123"    },
                { "Revolut-Signature",         "v1=hexhex" },
            },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Ignored);
    }
}

// ─── Tpay ────────────────────────────────────────────────────────────────────

public class TpayWebhookTests
{
    private static TpayProvider Build(ITpayServiceCaller caller) => new(caller);

    [Fact]
    public async Task HandleWebhook_ValidSignature_PAID_ReturnsOk_Finished()
    {
        var body = "id=1010&tr_id=tpay_1&tr_status=PAID";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotificationAsync(body, "sig_val", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "X-JWS-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_FALSE_ReturnsOk_Rejected()
    {
        var body = "id=1010&tr_id=tpay_1&tr_status=FALSE";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotificationAsync(body, "sig_val", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "X-JWS-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Ignored);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsFail()
    {
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Build(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = "tr_status=TRUE",
            Headers = new Dictionary<string, StringValues> { { "X-JWS-Signature", "bad" } },
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_UnknownStatus_ReturnsIgnore()
    {
        var body = "id=1010&tr_id=tpay_1&tr_status=pending";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotificationAsync(body, "sig_val", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "X-JWS-Signature", "sig_val" } },
        };
        var result = await Build(mock.Object).HandleWebhookAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Ignored);
    }
}
