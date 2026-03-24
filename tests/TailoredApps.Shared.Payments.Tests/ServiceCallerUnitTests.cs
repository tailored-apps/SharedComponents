using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
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

file static class CallerHelper
{
    public static IHttpClientFactory DummyFactory()
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return mock.Object;
    }
}

// ─── PayUServiceCaller ────────────────────────────────────────────────────────

/// <summary>Unit testy dla PayUServiceCaller — czyste funkcje (bez HTTP).</summary>
public class PayUServiceCallerTests
{
    private static PayUServiceCaller Build(string signatureKey) =>
        new(Options.Create(new PayUServiceOptions
        {
            SignatureKey = signatureKey,
            ServiceUrl   = "https://secure.snd.payu.com",
        }), CallerHelper.DummyFactory());

    [Fact]
    public void VerifySignature_MD5_Valid()
    {
        const string body = "{\"order\":{\"status\":\"COMPLETED\"}}";
        const string key  = "test_sig_key";
        var caller = Build(key);
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(body + key))).ToLowerInvariant();
        var sig  = $"sender=checkout;signature={hash};algorithm=MD5;content=DOCUMENT";
        Assert.True(caller.VerifySignature(body, sig));
    }

    [Fact]
    public void VerifySignature_SHA256_Valid()
    {
        const string body = "{\"order\":{\"status\":\"COMPLETED\"}}";
        const string key  = "test_sig_key";
        var caller = Build(key);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body + key))).ToLowerInvariant();
        var sig  = $"sender=checkout;signature={hash};algorithm=SHA256;content=DOCUMENT";
        Assert.True(caller.VerifySignature(body, sig));
    }

    [Fact]
    public void VerifySignature_SHA_256_Alias_Valid()
    {
        const string body = "{\"data\":\"test\"}";
        const string key  = "key123";
        var caller = Build(key);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body + key))).ToLowerInvariant();
        var sig  = $"sender=checkout;signature={hash};algorithm=SHA-256;content=DOCUMENT";
        Assert.True(caller.VerifySignature(body, sig));
    }

    [Fact]
    public void VerifySignature_WrongHash_ReturnsFalse()
    {
        var caller = Build("correct_key");
        var sig = "sender=checkout;signature=deadbeef00000000000000000000000000000000;algorithm=MD5;content=DOCUMENT";
        Assert.False(caller.VerifySignature("{}", sig));
    }

    [Fact]
    public void VerifySignature_MissingSignaturePart_ReturnsFalse()
    {
        var caller = Build("key");
        Assert.False(caller.VerifySignature("{}", "sender=checkout;algorithm=MD5"));
    }

    [Fact]
    public void VerifySignature_EmptyString_ReturnsFalse()
    {
        var caller = Build("key");
        Assert.False(caller.VerifySignature("{}", ""));
    }
}

// ─── HotPayServiceCaller ──────────────────────────────────────────────────────

/// <summary>Unit testy dla HotPayServiceCaller — czyste funkcje (bez HTTP).</summary>
public class HotPayServiceCallerTests
{
    private static HotPayServiceCaller Build(string secretHash) =>
        new(Options.Create(new HotPayServiceOptions
        {
            SecretHash = secretHash,
            ServiceUrl = "https://platnosci.hotpay.pl",
            ReturnUrl  = "https://example.com/return",
        }), CallerHelper.DummyFactory());

    [Fact]
    public void VerifyNotification_ValidHash_ReturnsTrue()
    {
        const string secret = "hotpay_secret";
        var caller = Build(secret);
        const string kwota = "9.99";
        const string id    = "pay_123";
        const string status = "SUCCESS";
        var data = $"{secret};{kwota};{id};{status}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        Assert.True(caller.VerifyNotification(hash, kwota, id, status));
    }

    [Fact]
    public void VerifyNotification_InvalidHash_ReturnsFalse()
    {
        var caller = Build("secret");
        Assert.False(caller.VerifyNotification("badhash", "9.99", "pay_1", "SUCCESS"));
    }

    [Fact]
    public void VerifyNotification_WrongSecret_ReturnsFalse()
    {
        var caller = Build("wrong_secret");
        const string data = "correct_secret;9.99;pay_1;SUCCESS";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        Assert.False(caller.VerifyNotification(hash, "9.99", "pay_1", "SUCCESS"));
    }

    [Fact]
    public void VerifyNotification_DifferentStatus_HashMismatch_ReturnsFalse()
    {
        const string secret = "sec";
        var caller = Build(secret);
        var correctData = $"{secret};10.00;id1;SUCCESS";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(correctData))).ToLowerInvariant();
        Assert.False(caller.VerifyNotification(hash, "10.00", "id1", "FAILURE"));
    }
}

// ─── PayNowServiceCaller ──────────────────────────────────────────────────────

/// <summary>Unit testy dla PayNowServiceCaller — czyste funkcje (bez HTTP).</summary>
public class PayNowServiceCallerTests
{
    private static PayNowServiceCaller Build(string sigKey) =>
        new(Options.Create(new PayNowServiceOptions
        {
            SignatureKey = sigKey,
            ApiKey       = "api_key",
            ServiceUrl   = "https://api.sandbox.paynow.pl",
        }), CallerHelper.DummyFactory());

    [Fact]
    public void VerifySignature_ValidHmac_ReturnsTrue()
    {
        const string key  = "paynow_sig_key";
        const string body = "{\"paymentId\":\"pn_1\",\"status\":\"CONFIRMED\"}";
        var caller = Build(key);
        var computed = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(body)));
        Assert.True(caller.VerifySignature(body, computed));
    }

    [Fact]
    public void VerifySignature_InvalidHmac_ReturnsFalse()
    {
        var caller = Build("key123");
        Assert.False(caller.VerifySignature("{}", "invalidsig=="));
    }

    [Fact]
    public void VerifySignature_EmptySignature_ReturnsFalse()
    {
        var caller = Build("key");
        Assert.False(caller.VerifySignature("{}", ""));
    }

    [Fact]
    public void VerifySignature_WrongKey_ReturnsFalse()
    {
        const string body = "{\"data\":\"test\"}";
        var callerCorrect = Build("correct_key");
        var hmac = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("correct_key"),
            Encoding.UTF8.GetBytes(body)));
        var callerWrong = Build("wrong_key");
        Assert.False(callerWrong.VerifySignature(body, hmac));
    }
}

// ─── RevolutServiceCaller ─────────────────────────────────────────────────────

/// <summary>Unit testy dla RevolutServiceCaller — czyste funkcje (bez HTTP).</summary>
public class RevolutServiceCallerTests
{
    private static RevolutServiceCaller Build(string webhookSecret) =>
        new(Options.Create(new RevolutServiceOptions
        {
            WebhookSecret = webhookSecret,
            ApiKey        = "sk_sandbox",
            ApiUrl        = "https://sandbox-merchant.revolut.com/api",
        }), CallerHelper.DummyFactory());

    private static string ComputeRevolutSig(string secret, string timestamp, string payload)
    {
        var signed = $"v1:{timestamp}.{payload}";
        var hex = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        return $"v1={hex}";
    }

    [Fact]
    public void VerifyWebhookSignature_Valid_ReturnsTrue()
    {
        const string secret = "revolut_webhook_secret";
        const string ts     = "1711234567";
        const string body   = "{\"event\":\"ORDER_COMPLETED\"}";
        var sig = ComputeRevolutSig(secret, ts, body);
        var caller = Build(secret);
        Assert.True(caller.VerifyWebhookSignature(body, ts, sig));
    }

    [Fact]
    public void VerifyWebhookSignature_WrongTimestamp_ReturnsFalse()
    {
        const string secret = "revolut_webhook_secret";
        const string body   = "{\"event\":\"ORDER_COMPLETED\"}";
        var sig = ComputeRevolutSig(secret, "1111111111", body);
        var caller = Build(secret);
        Assert.False(caller.VerifyWebhookSignature(body, "9999999999", sig));
    }

    [Fact]
    public void VerifyWebhookSignature_InvalidSignature_ReturnsFalse()
    {
        var caller = Build("secret");
        Assert.False(caller.VerifyWebhookSignature("{}", "123", "v1=badhex"));
    }

    [Fact]
    public void VerifyWebhookSignature_NoV1Prefix_StillVerifies()
    {
        const string secret = "sec";
        const string ts     = "12345";
        const string body   = "{\"test\":1}";
        var signed = $"v1:{ts}.{body}";
        var hex = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        // Without v1= prefix
        var caller = Build(secret);
        Assert.True(caller.VerifyWebhookSignature(body, ts, hex));
    }

    [Fact]
    public void VerifyWebhookSignature_WrongSecret_ReturnsFalse()
    {
        const string ts   = "1234";
        const string body = "{\"ev\":\"x\"}";
        var sig = ComputeRevolutSig("correct_secret", ts, body);
        var caller = Build("wrong_secret");
        Assert.False(caller.VerifyWebhookSignature(body, ts, sig));
    }
}

// ─── AdyenServiceCaller ───────────────────────────────────────────────────────

/// <summary>Unit testy dla AdyenServiceCaller — czyste funkcje (bez HTTP).</summary>
public class AdyenServiceCallerTests
{
    private static AdyenServiceCaller Build(string hmacKeyHex) =>
        new(Options.Create(new AdyenServiceOptions
        {
            ApiKey              = "AQE...",
            MerchantAccount     = "TestMerchant",
            NotificationHmacKey = hmacKeyHex,
            CheckoutUrl         = "https://checkout-test.adyen.com/v71",
            Environment         = "test",
        }), CallerHelper.DummyFactory());

    private static (string hex, string b64) ComputeAdyenHmac(string hexKey, string payload)
    {
        var keyBytes  = Convert.FromHexString(hexKey);
        var dataBytes = Encoding.UTF8.GetBytes(payload);
        var raw       = HMACSHA256.HashData(keyBytes, dataBytes);
        return (Convert.ToHexString(raw).ToLowerInvariant(), Convert.ToBase64String(raw));
    }

    [Fact]
    public void VerifyNotificationHmac_Valid_ReturnsTrue()
    {
        const string hexKey  = "4142434445464748494a4b4c4d4e4f50"; // 16 bytes
        const string payload = "{\"notif\":\"test\"}";
        var (_, b64) = ComputeAdyenHmac(hexKey, payload);
        var caller = Build(hexKey);
        Assert.True(caller.VerifyNotificationHmac(payload, b64));
    }

    [Fact]
    public void VerifyNotificationHmac_InvalidSig_ReturnsFalse()
    {
        var caller = Build("4142434445464748494a4b4c4d4e4f50");
        Assert.False(caller.VerifyNotificationHmac("{}", "badsignature=="));
    }

    [Fact]
    public void VerifyNotificationHmac_InvalidHexKey_ReturnsFalse()
    {
        var caller = Build("not-valid-hex!");
        Assert.False(caller.VerifyNotificationHmac("{}", "anything=="));
    }

    [Fact]
    public void VerifyNotificationHmac_WrongKey_ReturnsFalse()
    {
        const string payload = "{\"data\":\"abc\"}";
        var (_, b64) = ComputeAdyenHmac("4142434445464748494a4b4c4d4e4f50", payload);
        var caller   = Build("5152535455565758595a5b5c5d5e5f60"); // different key
        Assert.False(caller.VerifyNotificationHmac(payload, b64));
    }

    [Fact]
    public void VerifyNotificationHmac_DifferentPayload_ReturnsFalse()
    {
        const string hexKey   = "4142434445464748494a4b4c4d4e4f50";
        var (_, b64) = ComputeAdyenHmac(hexKey, "{\"original\":true}");
        var caller   = Build(hexKey);
        Assert.False(caller.VerifyNotificationHmac("{\"tampered\":true}", b64));
    }
}

// ─── TpayServiceCaller ────────────────────────────────────────────────────────

/// <summary>Unit testy dla TpayServiceCaller — czyste funkcje (bez HTTP).</summary>
public class TpayServiceCallerTests
{
    private static TpayServiceCaller Build(string securityCode) =>
        new(Options.Create(new TpayServiceOptions
        {
            SecurityCode = securityCode,
            ClientId     = "client_1",
            ClientSecret = "secret",
            ServiceUrl   = "https://openapi.sandbox.tpay.com",
        }), CallerHelper.DummyFactory());

    [Fact]
    public void VerifyNotification_ValidSig_ReturnsTrue()
    {
        const string code = "tpay_security";
        const string body = "{\"id\":\"txn_1\",\"status\":\"paid\"}";
        var caller  = Build(code);
        var hash    = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body + code))).ToLowerInvariant();
        Assert.True(caller.VerifyNotification(body, hash));
    }

    [Fact]
    public void VerifyNotification_InvalidSig_ReturnsFalse()
    {
        var caller = Build("tpay_sec");
        Assert.False(caller.VerifyNotification("{}", "badsig"));
    }

    [Fact]
    public void VerifyNotification_WrongCode_ReturnsFalse()
    {
        const string body = "{\"status\":\"paid\"}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body + "correct_code"))).ToLowerInvariant();
        var caller = Build("wrong_code");
        Assert.False(caller.VerifyNotification(body, hash));
    }

    [Fact]
    public void VerifyNotification_EmptySig_ReturnsFalse()
    {
        var caller = Build("code");
        Assert.False(caller.VerifyNotification("{}", ""));
    }
}

// ─── Przelewy24ServiceCaller ──────────────────────────────────────────────────

/// <summary>Unit testy dla Przelewy24ServiceCaller — czyste funkcje (bez HTTP).</summary>
public class Przelewy24ServiceCallerTests
{
    private static Przelewy24ServiceCaller Build(string crcKey, int merchantId = 12345) =>
        new(Options.Create(new Przelewy24ServiceOptions
        {
            CrcKey     = crcKey,
            MerchantId = merchantId,
            PosId      = merchantId,
            ApiKey     = "api_key",
            ServiceUrl = "https://sandbox.przelewy24.pl",
            NotifyUrl  = "https://example.com/notify",
            ReturnUrl  = "https://example.com/return",
        }), CallerHelper.DummyFactory());

    private static string ComputeP24Sign(string sessionId, int merchantId, long amount, string currency, string crcKey)
    {
        var json = JsonSerializer.Serialize(new
        {
            sessionId,
            merchantId,
            amount,
            currency,
            crc = crcKey,
        });
        return Convert.ToHexString(SHA384.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    [Fact]
    public void ComputeSign_ReturnsCorrectSHA384()
    {
        const string crc = "p24_crc_key";
        var caller  = Build(crc, 12345);
        var sign    = caller.ComputeSign("sess_1", 12345, 999, "PLN");
        var expected = ComputeP24Sign("sess_1", 12345, 999, "PLN", crc);
        Assert.Equal(expected, sign);
    }

    [Fact]
    public void ComputeSign_DifferentInputs_DifferentSigns()
    {
        const string crc = "crc";
        var caller = Build(crc);
        var s1 = caller.ComputeSign("sess_1", 12345, 999, "PLN");
        var s2 = caller.ComputeSign("sess_2", 12345, 999, "PLN");
        Assert.NotEqual(s1, s2);
    }

    [Fact]
    public void VerifyNotification_ValidSign_ReturnsTrue()
    {
        const string crc      = "p24crc";
        const int    merchant = 12345;
        var caller = Build(crc, merchant);

        const string sessionId = "test_sess";
        const int    orderId   = 99;
        const long   amount    = 1000L;
        const string currency  = "PLN";

        var json = JsonSerializer.Serialize(new
        {
            sessionId,
            orderId,
            merchantId = merchant,
            amount,
            currency,
            crc,
        });
        var sign = Convert.ToHexString(SHA384.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        var body = JsonSerializer.Serialize(new
        {
            sessionId,
            orderId,
            merchantId = merchant,
            amount,
            currency,
            sign,
        });

        Assert.True(caller.VerifyNotification(body));
    }

    [Fact]
    public void VerifyNotification_WrongSign_ReturnsFalse()
    {
        var caller = Build("crc");
        var body   = JsonSerializer.Serialize(new
        {
            sessionId  = "s",
            orderId    = 1,
            merchantId = 12345,
            amount     = 100L,
            currency   = "PLN",
            sign       = "wrongsignature",
        });
        Assert.False(caller.VerifyNotification(body));
    }

    [Fact]
    public void VerifyNotification_MissingSign_ReturnsFalse()
    {
        var caller = Build("crc");
        var body = JsonSerializer.Serialize(new { sessionId = "s", orderId = 1 });
        Assert.False(caller.VerifyNotification(body));
    }

    [Fact]
    public void VerifyNotification_MalformedJson_ReturnsFalse()
    {
        var caller = Build("crc");
        Assert.False(caller.VerifyNotification("not-json-at-all"));
    }
}
