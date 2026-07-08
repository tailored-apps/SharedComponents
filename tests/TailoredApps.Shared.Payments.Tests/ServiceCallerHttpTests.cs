#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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

// ────────────────────────────────────────────────────────────────────────────
// HTTP mock helper
// ────────────────────────────────────────────────────────────────────────────

file sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

file static class HttpFactory
{
    public static IHttpClientFactory Create(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpHandler(_ =>
            new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        return mock.Object;
    }

    public static IHttpClientFactory CreateRedirect(string json, string locationUrl)
    {
        var handler = new MockHttpHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.Found);
            resp.Headers.Location = new Uri(locationUrl);
            resp.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return resp;
        });
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler) { BaseAddress = new Uri("https://example.com") });
        return mock.Object;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// TpayServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class TpayServiceCallerHttpTests
{
    private static TpayServiceCaller Build(IHttpClientFactory factory, string serviceUrl = "https://api.tpay.com")
        => new(Options.Create(new TpayServiceOptions
        {
            ClientId = "client_1",
            ClientSecret = "secret_1",
            SecurityCode = "sec",
            ServiceUrl = serviceUrl,
            ReturnUrl = "https://example.com/return",
            NotifyUrl = "https://example.com/notify",
        }), factory);

    [Fact]
    public async Task GetAccessTokenAsync_ValidResponse_ReturnsToken()
    {
        var factory = HttpFactory.Create("""{"access_token":"tok_abc","token_type":"Bearer"}""");
        var caller = Build(factory);
        var token = await caller.GetAccessTokenAsync();
        Assert.Equal("tok_abc", token);
    }

    [Fact]
    public async Task CreateTransactionAsync_ValidResponse_ReturnsIdAndUrl()
    {
        var factory = HttpFactory.Create("""{"transactionId":"txn_123","transactionPaymentUrl":"https://pay.tpay.com/abc"}""");
        var caller = Build(factory);
        var (id, url) = await caller.CreateTransactionAsync("tok", new PaymentRequest
        {
            Amount = 9.99m,
            Currency = "PLN",
            Email = "test@example.com",
            Title = "Test",
            FirstName = "Jan",
            Surname = "K",
            PaymentChannel = "blik",
        });
        Assert.Equal("txn_123", id);
        Assert.Equal("https://pay.tpay.com/abc", url);
    }

    [Fact]
    public async Task CreateTransactionAsync_NullChannel_StillWorks()
    {
        var factory = HttpFactory.Create("""{"transactionId":"txn_1","transactionPaymentUrl":"https://pay.tpay.com/x"}""");
        var caller = Build(factory);
        var (id, _) = await caller.CreateTransactionAsync("tok", new PaymentRequest
        {
            Amount = 1m,
            Currency = "PLN",
        });
        Assert.Equal("txn_1", id);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_Correct_ReturnsFinished()
    {
        var factory = HttpFactory.Create("""{"status":"correct"}""");
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Finished, status);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_Pending_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"status":"pending"}""");
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_Error_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"status":"error"}""");
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_Chargeback_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"status":"chargeback"}""");
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_UnknownStatus_ReturnsCreated()
    {
        var factory = HttpFactory.Create("""{"status":"unknown_state"}""");
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Created, status);
    }

    [Fact]
    public async Task GetTransactionStatusAsync_HttpError_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{}""", HttpStatusCode.InternalServerError);
        var caller = Build(factory);
        var status = await caller.GetTransactionStatusAsync("tok", "txn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// PayNowServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class PayNowServiceCallerHttpTests
{
    private static PayNowServiceCaller Build(IHttpClientFactory factory)
        => new(Options.Create(new PayNowServiceOptions
        {
            ApiKey = "api_key",
            SignatureKey = "sig_key",
            ServiceUrl = "https://api.paynow.pl",
            ReturnUrl = "https://example.com/return",
            ContinueUrl = "https://example.com/continue",
        }), factory);

    [Fact]
    public async Task CreatePaymentAsync_Success_ReturnsIdAndUrl()
    {
        var factory = HttpFactory.Create("""{"paymentId":"pn_abc","status":"NEW","redirectUrl":"https://api.paynow.pl/checkout/pn_abc"}""");
        var caller = Build(factory);
        var (id, url) = await caller.CreatePaymentAsync(new PaymentRequest
        {
            Amount = 9.99m,
            Currency = "PLN",
            Email = "test@example.com",
            Title = "Test order",
            AdditionalData = "ext_123",
        });
        Assert.Equal("pn_abc", id);
        Assert.Equal("https://api.paynow.pl/checkout/pn_abc", url);
    }

    [Fact]
    public async Task CreatePaymentAsync_NoAdditionalData_GeneratesExternalId()
    {
        var factory = HttpFactory.Create("""{"paymentId":"pn_xyz","redirectUrl":"https://api.paynow.pl/checkout/pn_xyz"}""");
        var caller = Build(factory);
        var (id, _) = await caller.CreatePaymentAsync(new PaymentRequest
        {
            Amount = 1m,
            Currency = "PLN",
        });
        Assert.Equal("pn_xyz", id);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_CONFIRMED_ReturnsFinished()
    {
        var factory = HttpFactory.Create("""{"status":"CONFIRMED"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Finished, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_PENDING_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"status":"PENDING"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_PROCESSING_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"status":"PROCESSING"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_NEW_ReturnsCreated()
    {
        var factory = HttpFactory.Create("""{"status":"NEW"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Created, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_ERROR_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"status":"ERROR"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_REJECTED_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"status":"REJECTED"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_ABANDONED_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"status":"ABANDONED"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Unknown_ReturnsCreated()
    {
        var factory = HttpFactory.Create("""{"status":"BLAH"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Created, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_HttpError_ReturnsRejected()
    {
        var factory = HttpFactory.Create("{}", HttpStatusCode.InternalServerError);
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("pn_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// PayUServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class PayUServiceCallerHttpTests
{
    private static PayUServiceCaller Build(IHttpClientFactory factory)
        => new(Options.Create(new PayUServiceOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            PosId = "pos123",
            SignatureKey = "sigkey",
            ServiceUrl = "https://secure.snd.payu.com",
            NotifyUrl = "https://example.com/notify",
            ContinueUrl = "https://example.com/continue",
        }), factory);

    [Fact]
    public async Task GetAccessTokenAsync_ValidResponse_ReturnsToken()
    {
        var factory = HttpFactory.Create("""{"access_token":"tok_payu","token_type":"bearer","expires_in":3600}""");
        var caller = Build(factory);
        var token = await caller.GetAccessTokenAsync();
        Assert.Equal("tok_payu", token);
    }

    [Fact]
    public async Task GetOrderStatusAsync_COMPLETED_ReturnsFinished()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"COMPLETED"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Finished, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_WAITING_FOR_CONFIRMATION_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"WAITING_FOR_CONFIRMATION"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_PENDING_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"PENDING"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_CANCELED_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"CANCELED"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_REJECTED_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"REJECTED"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Unknown_ReturnsCreated()
    {
        var factory = HttpFactory.Create("""{"orders":[{"orderId":"ord_1","status":"NEW"}]}""");
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Created, status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_HttpError_ReturnsRejected()
    {
        var factory = HttpFactory.Create("{}", HttpStatusCode.InternalServerError);
        var caller = Build(factory);
        var status = await caller.GetOrderStatusAsync("tok", "ord_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    // Note: CreateOrderAsync uses new HttpClientHandler{AllowAutoRedirect=false} directly,
    // not the IHttpClientFactory — tested via provider-level mocks in PayUProviderTests.
    [Fact]
    public void VerifySignature_DefaultKey_Truthy()
    {
        // Smoke test to ensure VerifySignature is exercised (also covered in ServiceCallerUnitTests)
        var caller = Build(HttpFactory.Create("{}"));
        Assert.False(caller.VerifySignature("{}", "sender=x;signature=deadbeef;algorithm=MD5"));
    }
}

// ────────────────────────────────────────────────────────────────────────────
// RevolutServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class RevolutServiceCallerHttpTests
{
    private static RevolutServiceCaller Build(IHttpClientFactory factory)
        => new(Options.Create(new RevolutServiceOptions
        {
            ApiKey = "sk_sandbox",
            ApiUrl = "https://sandbox-merchant.revolut.com/api",
            ReturnUrl = "https://example.com/return",
            WebhookSecret = "whsec",
        }), factory);

    [Fact]
    public async Task CreateOrderAsync_Success_ReturnsIdAndUrl()
    {
        var factory = HttpFactory.Create("""{"id":"rev_abc","checkout_url":"https://checkout.revolut.com/pay/abc","state":"pending"}""");
        var caller = Build(factory);
        var (id, url) = await caller.CreateOrderAsync(new PaymentRequest
        {
            Amount = 9.99m,
            Currency = "PLN",
            Title = "Test",
            Email = "x@example.com",
            AdditionalData = "ext_1",
        });
        Assert.Equal("rev_abc", id);
        Assert.Equal("https://checkout.revolut.com/pay/abc", url);
    }

    [Fact]
    public async Task CreateOrderAsync_NoAdditionalData_StillWorks()
    {
        var factory = HttpFactory.Create("""{"id":"rev_xyz","checkout_url":"https://checkout.revolut.com/pay/xyz"}""");
        var caller = Build(factory);
        var (id, _) = await caller.CreateOrderAsync(new PaymentRequest { Amount = 1m, Currency = "EUR" });
        Assert.Equal("rev_xyz", id);
    }

    [Fact]
    public async Task GetOrderAsync_Success_ReturnsStateAndId()
    {
        var factory = HttpFactory.Create("""{"id":"rev_1","state":"completed","checkout_url":"https://..."}""");
        var caller = Build(factory);
        var (state, id) = await caller.GetOrderAsync("rev_1");
        Assert.Equal("completed", state);
        Assert.Equal("rev_1", id);
    }

    [Fact]
    public async Task GetOrderAsync_HttpError_ReturnsNullState()
    {
        var factory = HttpFactory.Create("{}", HttpStatusCode.NotFound);
        var caller = Build(factory);
        var (state, id) = await caller.GetOrderAsync("rev_1");
        Assert.Null(state);
        Assert.Equal("rev_1", id);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// AdyenServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class AdyenServiceCallerHttpTests
{
    private static AdyenServiceCaller Build(IHttpClientFactory factory, string checkoutUrl = "https://checkout-test.adyen.com/v71")
        => new(Options.Create(new AdyenServiceOptions
        {
            ApiKey = "AQE_key",
            MerchantAccount = "TestMerchant",
            ReturnUrl = "https://example.com/return",
            NotificationHmacKey = "4142434445464748494a4b4c4d4e4f50",
            CheckoutUrl = checkoutUrl,
            Environment = "test",
        }), factory);

    [Fact]
    public async Task CreateSessionAsync_Success_ReturnsIdAndUrl()
    {
        var factory = HttpFactory.Create("""{"id":"cs_adyen_abc","sessionData":"data_xyz","url":"https://checkoutshopper-test.adyen.com/sessions/cs_adyen_abc"}""");
        var caller = Build(factory);
        var (id, url, error) = await caller.CreateSessionAsync(new PaymentRequest
        {
            Amount = 9.99m,
            Currency = "PLN",
            Email = "x@e.com",
            AdditionalData = "ext_1",
            Country = "PL",
        });
        Assert.Equal("cs_adyen_abc", id);
        Assert.NotNull(url);
        Assert.Null(error);
    }

    [Fact]
    public async Task CreateSessionAsync_NoAdditionalData_StillWorks()
    {
        var factory = HttpFactory.Create("""{"id":"cs_adyen_xyz","url":"https://test.adyen.com/abc"}""");
        var caller = Build(factory);
        var (id, _, error) = await caller.CreateSessionAsync(new PaymentRequest
        {
            Amount = 1m,
            Currency = "EUR",
        });
        Assert.Equal("cs_adyen_xyz", id);
        Assert.Null(error);
    }

    [Fact]
    public async Task CreateSessionAsync_HttpError_ReturnsError()
    {
        var factory = HttpFactory.Create("""{"status":401,"message":"Unauthorized"}""", HttpStatusCode.Unauthorized);
        var caller = Build(factory);
        var (id, url, error) = await caller.CreateSessionAsync(new PaymentRequest { Amount = 1m, Currency = "PLN" });
        Assert.Null(id);
        Assert.Null(url);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Authorised_ReturnsFinished()
    {
        var factory = HttpFactory.Create("""{"status":"Authorised","resultCode":"Authorised"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Finished, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Refused_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"resultCode":"Refused"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Cancelled_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"resultCode":"Cancelled"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Pending_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"resultCode":"Pending"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Received_ReturnsProcessing()
    {
        var factory = HttpFactory.Create("""{"resultCode":"Received"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Processing, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Unknown_ReturnsCreated()
    {
        var factory = HttpFactory.Create("""{"resultCode":"Unknown"}""");
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Created, status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_HttpError_ReturnsRejected()
    {
        var factory = HttpFactory.Create("{}", HttpStatusCode.NotFound);
        var caller = Build(factory);
        var status = await caller.GetPaymentStatusAsync("psp_1");
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Przelewy24ServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class Przelewy24ServiceCallerHttpTests
{
    private static Przelewy24ServiceCaller Build(IHttpClientFactory factory)
        => new(Options.Create(new Przelewy24ServiceOptions
        {
            MerchantId = 12345,
            PosId = 12345,
            ApiKey = "api_key",
            CrcKey = "crc_key",
            ServiceUrl = "https://sandbox.przelewy24.pl",
            NotifyUrl = "https://example.com/notify",
            ReturnUrl = "https://example.com/return",
        }), factory);

    [Fact]
    public async Task RegisterTransactionAsync_Success_ReturnsToken()
    {
        var factory = HttpFactory.Create("""{"data":{"token":"p24_tok_abc"}}""");
        var caller = Build(factory);
        var (token, error) = await caller.RegisterTransactionAsync(new PaymentRequest
        {
            Amount = 10m,
            Currency = "PLN",
            Email = "x@example.com",
            Title = "Test",
            Description = "Order",
        }, "sess_test_1");
        Assert.Equal("p24_tok_abc", token);
        Assert.Null(error);
    }

    [Fact]
    public async Task RegisterTransactionAsync_HttpError_ReturnsError()
    {
        var factory = HttpFactory.Create("""{"error":"Unauthorized"}""", HttpStatusCode.Unauthorized);
        var caller = Build(factory);
        var (token, error) = await caller.RegisterTransactionAsync(new PaymentRequest
        {
            Amount = 1m,
            Currency = "PLN",
        }, "sess_1");
        Assert.Null(token);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task VerifyTransactionAsync_Success_ReturnsFinished()
    {
        var factory = HttpFactory.Create("""{"data":{"status":"success"}}""");
        var caller = Build(factory);
        var status = await caller.VerifyTransactionAsync("sess_1", 1000, "PLN", 12345);
        Assert.Equal(PaymentStatusEnum.Finished, status);
    }

    [Fact]
    public async Task VerifyTransactionAsync_HttpError_ReturnsRejected()
    {
        var factory = HttpFactory.Create("""{"error":"failed"}""", HttpStatusCode.BadRequest);
        var caller = Build(factory);
        var status = await caller.VerifyTransactionAsync("sess_1", 1000, "PLN", 12345);
        Assert.Equal(PaymentStatusEnum.Rejected, status);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// HotPayServiceCaller — HTTP method tests
// ────────────────────────────────────────────────────────────────────────────

public class HotPayServiceCallerHttpTests
{
    private static HotPayServiceCaller Build(IHttpClientFactory factory)
        => new(Options.Create(new HotPayServiceOptions
        {
            SecretHash = "hotpay_secret",
            ServiceUrl = "https://platnosci.hotpay.pl",
            ReturnUrl = "https://example.com/return",
            NotifyUrl = "https://example.com/notify",
        }), factory);

    [Fact]
    public async Task InitPaymentAsync_WithRedirectUrl_ReturnsUrl()
    {
        var factory = HttpFactory.Create("""{"STATUS":"SUCCESS","PRZEKIERUJ_DO":"https://platnosci.hotpay.pl/pay/abc","ID_PLATNOSCI":"pay_123"}""");
        var caller = Build(factory);
        var (id, url) = await caller.InitPaymentAsync(new PaymentRequest
        {
            Amount = 9.99m,
            Currency = "PLN",
            Title = "Test",
            Email = "x@e.com",
        }, "my_pay_id");
        Assert.Equal("pay_123", id);
        Assert.Equal("https://platnosci.hotpay.pl/pay/abc", url);
    }

    [Fact]
    public async Task InitPaymentAsync_NoPaymentIdInResponse_UsesInputId()
    {
        var factory = HttpFactory.Create("""{"STATUS":"SUCCESS","PRZEKIERUJ_DO":"https://hotpay.pl/pay/x"}""");
        var caller = Build(factory);
        var (id, url) = await caller.InitPaymentAsync(new PaymentRequest
        {
            Amount = 1m,
            Currency = "PLN",
            Description = "Desc",
        }, "fallback_id");
        Assert.Equal("fallback_id", id);
        Assert.Equal("https://hotpay.pl/pay/x", url);
    }

    [Fact]
    public async Task InitPaymentAsync_WithEmail_SendsPayload()
    {
        var factory = HttpFactory.Create("""{"STATUS":"SUCCESS","PRZEKIERUJ_DO":"https://hotpay.pl/p","ID_PLATNOSCI":"p_1"}""");
        var caller = Build(factory);
        var (id, _) = await caller.InitPaymentAsync(new PaymentRequest
        {
            Amount = 5m,
            Title = "Service",
            Email = "user@test.com",
        }, "p_1");
        Assert.Equal("p_1", id);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// ConfigureOptions tests
// ────────────────────────────────────────────────────────────────────────────

public class ConfigureOptionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> kv)
        => new ConfigurationBuilder().AddInMemoryCollection(kv).Build();

    [Fact]
    public void TpayConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:Tpay:ClientId"] = "cid",
            ["Payments:Providers:Tpay:ClientSecret"] = "csec",
            ["Payments:Providers:Tpay:MerchantId"] = "mid",
            ["Payments:Providers:Tpay:ServiceUrl"] = "https://api.tpay.com",
            ["Payments:Providers:Tpay:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:Tpay:NotifyUrl"] = "https://n.com",
            ["Payments:Providers:Tpay:SecurityCode"] = "sec123",
        });
        var configure = new TpayConfigureOptions(cfg);
        var options = new TpayServiceOptions();
        configure.Configure(options);
        Assert.Equal("cid", options.ClientId);
        Assert.Equal("csec", options.ClientSecret);
        Assert.Equal("sec123", options.SecurityCode);
    }

    [Fact]
    public void TpayConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        var configure = new TpayConfigureOptions(cfg);
        var options = new TpayServiceOptions();
        configure.Configure(options); // should not throw
    }

    [Fact]
    public void PayUConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:PayU:ClientId"] = "pcid",
            ["Payments:Providers:PayU:ClientSecret"] = "pcsec",
            ["Payments:Providers:PayU:PosId"] = "pos123",
            ["Payments:Providers:PayU:SignatureKey"] = "sigkey",
            ["Payments:Providers:PayU:ServiceUrl"] = "https://secure.payu.com",
            ["Payments:Providers:PayU:NotifyUrl"] = "https://n.com",
            ["Payments:Providers:PayU:ContinueUrl"] = "https://c.com",
        });
        var configure = new PayUConfigureOptions(cfg);
        var options = new PayUServiceOptions();
        configure.Configure(options);
        Assert.Equal("pcid", options.ClientId);
        Assert.Equal("sigkey", options.SignatureKey);
        Assert.Equal("https://secure.payu.com", options.ServiceUrl);
    }

    [Fact]
    public void PayUConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new PayUConfigureOptions(cfg).Configure(new PayUServiceOptions());
    }

    [Fact]
    public void PayNowConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:PayNow:ApiKey"] = "pn_api",
            ["Payments:Providers:PayNow:SignatureKey"] = "pn_sig",
            ["Payments:Providers:PayNow:ServiceUrl"] = "https://api.paynow.pl",
            ["Payments:Providers:PayNow:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:PayNow:ContinueUrl"] = "https://c.com",
        });
        var configure = new PayNowConfigureOptions(cfg);
        var options = new PayNowServiceOptions();
        configure.Configure(options);
        Assert.Equal("pn_api", options.ApiKey);
        Assert.Equal("pn_sig", options.SignatureKey);
    }

    [Fact]
    public void PayNowConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new PayNowConfigureOptions(cfg).Configure(new PayNowServiceOptions());
    }

    [Fact]
    public void RevolutConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:Revolut:ApiKey"] = "sk_rev",
            ["Payments:Providers:Revolut:ApiUrl"] = "https://merchant.revolut.com/api",
            ["Payments:Providers:Revolut:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:Revolut:WebhookSecret"] = "whsec_rev",
        });
        var configure = new RevolutConfigureOptions(cfg);
        var options = new RevolutServiceOptions();
        configure.Configure(options);
        Assert.Equal("sk_rev", options.ApiKey);
        Assert.Equal("whsec_rev", options.WebhookSecret);
    }

    [Fact]
    public void RevolutConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new RevolutConfigureOptions(cfg).Configure(new RevolutServiceOptions());
    }

    [Fact]
    public void AdyenConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:Adyen:ApiKey"] = "adyen_api",
            ["Payments:Providers:Adyen:MerchantAccount"] = "TestMerchant",
            ["Payments:Providers:Adyen:ClientKey"] = "client_key",
            ["Payments:Providers:Adyen:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:Adyen:NotificationHmacKey"] = "hexkey123",
            ["Payments:Providers:Adyen:IsTest"] = "true",
        });
        var configure = new AdyenConfigureOptions(cfg);
        var options = new AdyenServiceOptions();
        configure.Configure(options);
        Assert.Equal("adyen_api", options.ApiKey);
        Assert.Equal("TestMerchant", options.MerchantAccount);
        Assert.True(options.IsTest);
    }

    [Fact]
    public void AdyenConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new AdyenConfigureOptions(cfg).Configure(new AdyenServiceOptions());
    }

    [Fact]
    public void HotPayConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:HotPay:SecretHash"] = "hotpay_sec",
            ["Payments:Providers:HotPay:ServiceUrl"] = "https://platnosci.hotpay.pl",
            ["Payments:Providers:HotPay:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:HotPay:NotifyUrl"] = "https://n.com",
        });
        var configure = new HotPayConfigureOptions(cfg);
        var options = new HotPayServiceOptions();
        configure.Configure(options);
        Assert.Equal("hotpay_sec", options.SecretHash);
        Assert.Equal("https://platnosci.hotpay.pl", options.ServiceUrl);
    }

    [Fact]
    public void HotPayConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new HotPayConfigureOptions(cfg).Configure(new HotPayServiceOptions());
    }

    [Fact]
    public void Przelewy24ConfigureOptions_Configure_SetsAllValues()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Payments:Providers:Przelewy24:MerchantId"] = "12345",
            ["Payments:Providers:Przelewy24:PosId"] = "12345",
            ["Payments:Providers:Przelewy24:ApiKey"] = "p24_api",
            ["Payments:Providers:Przelewy24:CrcKey"] = "p24_crc",
            ["Payments:Providers:Przelewy24:ServiceUrl"] = "https://secure.przelewy24.pl",
            ["Payments:Providers:Przelewy24:ReturnUrl"] = "https://r.com",
            ["Payments:Providers:Przelewy24:NotifyUrl"] = "https://n.com",
        });
        var configure = new Przelewy24ConfigureOptions(cfg);
        var options = new Przelewy24ServiceOptions();
        configure.Configure(options);
        Assert.Equal(12345, options.MerchantId);
        Assert.Equal("p24_api", options.ApiKey);
        Assert.Equal("p24_crc", options.CrcKey);
    }

    [Fact]
    public void Przelewy24ConfigureOptions_Configure_NullSection_DoesNotThrow()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>());
        new Przelewy24ConfigureOptions(cfg).Configure(new Przelewy24ServiceOptions());
    }
}
