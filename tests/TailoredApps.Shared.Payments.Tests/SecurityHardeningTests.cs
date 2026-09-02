using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
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
using TailoredApps.Shared.Payments.Security;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

file static class Http
{
    public static IHttpClientFactory Dummy()
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return mock.Object;
    }
}

// ─── Core helpers ────────────────────────────────────────────────────────────

public class WebhookSignatureTests
{
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "ABC", false)]
    [InlineData("abc", "ab", false)]
    [InlineData("", "abc", false)]
    [InlineData("abc", "", false)]
    [InlineData(null, "abc", false)]
    [InlineData("abc", null, false)]
    public void When_FixedTimeEquals_Should_Compare_Exactly_And_Fail_On_Empty(string expected, string received, bool result)
        => Assert.Equal(result, WebhookSignature.FixedTimeEquals(expected, received));

    [Theory]
    [InlineData("deadbeef", "DEADBEEF", true)]
    [InlineData("deadbeef", "deadbeef", true)]
    [InlineData("deadbeef", "deadbeee", false)]
    [InlineData("", "", false)]
    public void When_FixedTimeEqualsIgnoreCase_Should_Ignore_Hex_Case(string expected, string received, bool result)
        => Assert.Equal(result, WebhookSignature.FixedTimeEqualsIgnoreCase(expected, received));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("s3cret", true)]
    public void When_IsSecretConfigured_Should_Reject_Blank(string secret, bool result)
        => Assert.Equal(result, WebhookSignature.IsSecretConfigured(secret));
}

public class PaymentIdentifierTests
{
    [Theory]
    [InlineData("ORD_123")]
    [InlineData("cs_test_a1B2")]
    [InlineData("ta_1.2-3")]
    [InlineData("8f2c0f7c1c3e4c0e9b4b0b6b2f1b4a3f")]
    public void When_Identifier_Is_Plain_Should_Be_Safe(string id)
    {
        Assert.True(PaymentIdentifier.IsSafe(id));
        Assert.Equal(id, PaymentIdentifier.EnsureSafe(id));
    }

    [Theory]
    [InlineData("../paymentchannels")]
    [InlineData("a/b")]
    [InlineData("a?sign=x")]
    [InlineData("a#frag")]
    [InlineData("a b")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(null)]
    public void When_Identifier_Contains_Url_Metacharacters_Should_Throw(string id)
    {
        Assert.False(PaymentIdentifier.IsSafe(id));
        Assert.Throws<ArgumentException>(() => PaymentIdentifier.EnsureSafe(id));
    }

    [Fact]
    public void When_Identifier_Is_Too_Long_Should_Throw()
    {
        var id = new string('a', PaymentIdentifier.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => PaymentIdentifier.EnsureSafe(id));
    }
}

public class PaymentWebhookRequestTests
{
    [Fact]
    public void When_Headers_Assigned_With_Different_Case_Should_Be_Found_Case_Insensitively()
    {
        var request = new PaymentWebhookRequest
        {
            Headers = new Dictionary<string, StringValues> { { "stripe-signature", "t=1,v1=abc" } },
            Query = new Dictionary<string, StringValues> { { "SIGN", "x" } },
        };

        Assert.True(request.Headers.TryGetValue("Stripe-Signature", out var sig));
        Assert.Equal("t=1,v1=abc", sig.ToString());
        Assert.True(request.Query.TryGetValue("sign", out var q));
        Assert.Equal("x", q.ToString());
    }

    [Fact]
    public void When_Headers_Not_Assigned_Should_Default_To_Empty_Case_Insensitive_Dictionary()
    {
        var request = new PaymentWebhookRequest();
        request.Headers["X-Test"] = "1";
        Assert.True(request.Headers.ContainsKey("x-test"));
    }
}

// ─── Fail-closed verification when secrets are missing ───────────────────────

public class MissingSecretFailsClosedTests
{
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Fact]
    public void PayU_EmptySignatureKey_RejectsForgedSignature()
    {
        const string body = "{\"order\":{\"status\":\"COMPLETED\"}}";
        var caller = new PayUServiceCaller(Options.Create(new PayUServiceOptions { SignatureKey = "" }), Http.Dummy());
        var forged = Hex(MD5.HashData(Encoding.UTF8.GetBytes(body)));
        Assert.False(caller.VerifySignature(body, $"sender=x;signature={forged};algorithm=MD5"));
    }

    [Fact]
    public void PayU_UnknownAlgorithm_IsRejected()
    {
        const string body = "{}";
        var caller = new PayUServiceCaller(Options.Create(new PayUServiceOptions { SignatureKey = "k" }), Http.Dummy());
        var hash = Hex(MD5.HashData(Encoding.UTF8.GetBytes(body + "k")));
        Assert.False(caller.VerifySignature(body, $"signature={hash};algorithm=CRC32"));
    }

    [Fact]
    public void PayU_Sha1_IsSupported()
    {
        const string body = "{}";
        var caller = new PayUServiceCaller(Options.Create(new PayUServiceOptions { SignatureKey = "k" }), Http.Dummy());
        var hash = Hex(SHA1.HashData(Encoding.UTF8.GetBytes(body + "k")));
        Assert.True(caller.VerifySignature(body, $"signature={hash};algorithm=SHA-1"));
    }

    [Fact]
    public void PayNow_EmptySignatureKey_RejectsForgedSignature()
    {
        const string body = "{\"status\":\"CONFIRMED\"}";
        var caller = new PayNowServiceCaller(Options.Create(new PayNowServiceOptions { SignatureKey = "" }), Http.Dummy());
        var forged = Convert.ToBase64String(HMACSHA256.HashData(Array.Empty<byte>(), Encoding.UTF8.GetBytes(body)));
        Assert.False(caller.VerifySignature(body, forged));
    }

    [Fact]
    public void HotPay_EmptySecret_RejectsForgedHash()
    {
        var caller = new HotPayServiceCaller(Options.Create(new HotPayServiceOptions { SecretHash = "" }), Http.Dummy());
        var forged = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(";9.99;id;SUCCESS")));
        Assert.False(caller.VerifyNotification(forged, "9.99", "id", "SUCCESS"));
    }

    [Fact]
    public void Adyen_EmptyHmacKey_RejectsForgedSignature()
    {
        const string body = "{}";
        var caller = new AdyenServiceCaller(Options.Create(new AdyenServiceOptions { NotificationHmacKey = "" }), Http.Dummy());
        var forged = Convert.ToBase64String(HMACSHA256.HashData(Array.Empty<byte>(), Encoding.UTF8.GetBytes(body)));
        Assert.False(caller.VerifyNotificationHmac(body, forged));
    }

    [Fact]
    public void Tpay_EmptySecurityCode_RejectsForgedSignature()
    {
        const string body = "{\"status\":\"correct\"}";
        var caller = new TpayServiceCaller(Options.Create(new TpayServiceOptions { SecurityCode = "" }), Http.Dummy());
        var forged = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        Assert.False(caller.VerifyNotification(body, forged));
    }

    [Fact]
    public void Przelewy24_EmptyCrc_RejectsForgedSign()
    {
        var caller = new Przelewy24ServiceCaller(Options.Create(new Przelewy24ServiceOptions { CrcKey = "", MerchantId = 1, PosId = 1 }), Http.Dummy());
        var json = JsonSerializer.Serialize(new { merchantId = 1, posId = 1, sessionId = "s", amount = 100, originAmount = 100, currency = "PLN", orderId = 1, methodId = 1, statement = "", crc = "" });
        var forged = Hex(SHA384.HashData(Encoding.UTF8.GetBytes(json)));
        var body = JsonSerializer.Serialize(new { merchantId = 1, posId = 1, sessionId = "s", amount = 100, originAmount = 100, currency = "PLN", orderId = 1, methodId = 1, statement = "", sign = forged });
        Assert.False(caller.VerifyNotification(body));
    }

    [Fact]
    public void Stripe_EmptyWebhookSecret_ThrowsInsteadOfVerifyingWithEmptyKey()
    {
        var caller = new StripeServiceCaller(
            Options.Create(new StripeServiceOptions { SecretKey = "sk_test_x", WebhookSecret = "" }),
            new global::Stripe.Checkout.SessionService());

        Assert.Throws<global::Stripe.StripeException>(() => caller.ConstructWebhookEvent("{}", "t=1,v1=abc"));
    }

    [Fact]
    public async Task Stripe_EmptyWebhookSecret_WebhookReturnsFail()
    {
        var caller = new StripeServiceCaller(
            Options.Create(new StripeServiceOptions { SecretKey = "sk_test_x", WebhookSecret = "" }),
            new global::Stripe.Checkout.SessionService());
        var provider = new StripeProvider(caller);

        var result = await provider.HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = "{}",
            Headers = new Dictionary<string, StringValues> { { "Stripe-Signature", "t=1,v1=abc" } },
        });

        Assert.False(result.Success);
        Assert.Contains("signature", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CashBill_EmptySecret_GetSignThrows()
    {
        var caller = new CashbillServiceCaller(Mock.Of<ICashbillHttpClient>(), Options.Create(new CashbillServiceOptions { ShopSecretPhrase = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            caller.GetSignForNotificationService(new TransactionStatusChanged { Command = "c", TransactionId = "t", Sign = "x" }));
    }
}

// ─── CashBill: no signature oracle, legacy path verified ─────────────────────

public class CashBillSignatureHardeningTests
{
    private static Mock<ICashbillServiceCaller> Caller(string expectedSign)
    {
        var mock = new Mock<ICashbillServiceCaller>();
        mock.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>())).ReturnsAsync(expectedSign);
        mock.Setup(c => c.GetPaymentStatus("TX_1")).ReturnsAsync(new PaymentStatus { Id = "TX_1", Status = "PositiveFinish" });
        return mock;
    }

    private static PaymentWebhookRequest Webhook(string sign) => new()
    {
        Query = new Dictionary<string, StringValues>
        {
            { "cmd", "transactionStatusChanged" },
            { "args", "TX_1" },
            { "sign", sign },
        },
    };

    [Fact]
    public async Task When_Webhook_Signature_Invalid_Should_Not_Disclose_Expected_Signature()
    {
        const string expected = "0123456789abcdef0123456789abcdef";
        var provider = new CashBillProvider(Caller(expected).Object);

        var result = await provider.HandleWebhookAsync(Webhook("ffffffffffffffffffffffffffffffff"));

        Assert.False(result.Success);
        Assert.Equal("Invalid signature.", result.ErrorMessage);
        Assert.DoesNotContain(expected, result.ErrorMessage);
    }

    [Fact]
    public async Task When_Webhook_Signature_Missing_Should_Fail()
    {
        var provider = new CashBillProvider(Caller("abc").Object);
        var result = await provider.HandleWebhookAsync(Webhook(string.Empty));
        Assert.False(result.Success);
        Assert.Equal("Missing signature.", result.ErrorMessage);
    }

    [Fact]
    public async Task When_Webhook_Signature_Valid_With_Different_Case_Should_Succeed()
    {
        var provider = new CashBillProvider(Caller("abcdef").Object);
        var result = await provider.HandleWebhookAsync(Webhook("ABCDEF"));
        Assert.True(result.Success);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse!.PaymentStatus);
    }

    [Fact]
    public async Task When_Secret_Not_Configured_Should_Fail_Closed()
    {
        var mock = new Mock<ICashbillServiceCaller>();
        mock.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>()))
            .ThrowsAsync(new InvalidOperationException("not configured"));
        var provider = new CashBillProvider(mock.Object);

        var result = await provider.HandleWebhookAsync(Webhook("abc"));

        Assert.False(result.Success);
        Assert.Equal("Signature verification is not configured.", result.ErrorMessage);
        mock.Verify(c => c.GetPaymentStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task When_Legacy_TransactionStatusChange_Signature_Invalid_Should_Reject_Without_Polling()
    {
        var mock = Caller("expected");
        var provider = new CashBillProvider(mock.Object);

        var result = await provider.TransactionStatusChange(new TransactionStatusChangePayload
        {
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "cmd", "transactionStatusChanged" },
                { "args", "TX_1" },
                { "sign", "forged" },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
        Assert.Equal("Invalid signature.", result.ResponseObject);
        mock.Verify(c => c.GetPaymentStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task When_Legacy_TransactionStatusChange_Missing_Args_Should_Reject()
    {
        var provider = new CashBillProvider(Caller("expected").Object);
        var result = await provider.TransactionStatusChange(new TransactionStatusChangePayload());
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task When_GetStatus_Called_With_Path_Traversal_Id_Should_Throw()
    {
        var caller = new CashbillServiceCaller(Mock.Of<ICashbillHttpClient>(), Options.Create(new CashbillServiceOptions
        {
            ShopSecretPhrase = "secret",
            ShopId = "shop",
            ServiceUrl = "https://pay.cashbill.pl/testws/rest/",
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => caller.GetPaymentStatus("../paymentchannels/shop"));
    }
}

// ─── Webhook results identify the payment ────────────────────────────────────

public class WebhookResultCarriesPaymentIdTests
{
    [Fact]
    public async Task PayNow_Confirmed_Carries_PaymentId()
    {
        const string body = "{\"paymentId\":\"PAY_1\",\"status\":\"CONFIRMED\"}";
        var mock = new Mock<IPayNowServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var result = await new PayNowProvider(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "signature", "sig" } },
        });
        Assert.True(result.Success);
        Assert.Equal("PAY_1", result.PaymentResponse!.PaymentUniqueId);
        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentResponse.PaymentStatus);
    }

    [Fact]
    public async Task PayU_Completed_Carries_OrderId()
    {
        const string body = "{\"order\":{\"orderId\":\"ORD_1\",\"status\":\"COMPLETED\"}}";
        var mock = new Mock<IPayUServiceCaller>();
        mock.Setup(m => m.VerifySignature(body, "sig")).Returns(true);
        var result = await new PayUProvider(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "openpayu-signature", "sig" } },
        });
        Assert.True(result.Success);
        Assert.Equal("ORD_1", result.PaymentResponse!.PaymentUniqueId);
    }

    [Fact]
    public async Task Revolut_Completed_Carries_OrderId()
    {
        const string body = "{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_1\"}";
        var mock = new Mock<IRevolutServiceCaller>();
        mock.Setup(m => m.VerifyWebhookSignature(body, "1", "sig")).Returns(true);
        var result = await new RevolutProvider(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", "1" },
                { "Revolut-Signature", "sig" },
            },
        });
        Assert.True(result.Success);
        Assert.Equal("rev_1", result.PaymentResponse!.PaymentUniqueId);
    }

    [Fact]
    public async Task Adyen_Authorisation_Carries_MerchantReference_And_Missing_Success_Is_Rejected()
    {
        const string body = "{\"notificationItems\":[{\"NotificationRequestItem\":{\"eventCode\":\"AUTHORISATION\",\"merchantReference\":\"ORDER-7\",\"pspReference\":\"psp_1\"}}]}";
        var mock = new Mock<IAdyenServiceCaller>();
        mock.Setup(m => m.VerifyNotificationHmac(body, "hmac")).Returns(true);
        var result = await new AdyenProvider(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "HmacSignature", "hmac" } },
        });
        Assert.True(result.Success);
        Assert.Equal("ORDER-7", result.PaymentResponse!.PaymentUniqueId);
        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentResponse.PaymentStatus);
    }

    [Fact]
    public async Task Przelewy24_Notification_Carries_SessionId()
    {
        const string body = "{\"sessionId\":\"sess_1\",\"amount\":100,\"currency\":\"PLN\",\"orderId\":5}";
        var mock = new Mock<IPrzelewy24ServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body)).Returns(true);
        mock.Setup(m => m.VerifyTransactionAsync("sess_1", 100, "PLN", 5)).ReturnsAsync(PaymentStatusEnum.Finished);
        var provider = new Przelewy24Provider(mock.Object, Options.Create(new Przelewy24ServiceOptions()));
        var result = await provider.HandleWebhookAsync(new PaymentWebhookRequest { Body = body });
        Assert.True(result.Success);
        Assert.Equal("sess_1", result.PaymentResponse!.PaymentUniqueId);
    }

    [Fact]
    public async Task Tpay_Notification_Carries_TransactionId()
    {
        const string body = "{\"transactionId\":\"ta_1\",\"status\":\"correct\"}";
        var mock = new Mock<ITpayServiceCaller>();
        mock.Setup(m => m.VerifyNotification(body, "sig")).Returns(true);
        var result = await new TpayProvider(mock.Object).HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "X-Signature", "sig" } },
        });
        Assert.True(result.Success);
        Assert.Equal("ta_1", result.PaymentResponse!.PaymentUniqueId);
    }
}
