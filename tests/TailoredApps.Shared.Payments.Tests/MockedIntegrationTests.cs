#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Moq;
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

/// <summary>
/// Mocked integration tests for all payment providers.
/// These tests replace real HTTP calls with Moq stubs so the full DI + PaymentService
/// pipeline can be exercised without a live sandbox environment.
///
/// When integration environments become available, create separate integration test projects
/// with real credentials instead of extending or removing this file.
/// </summary>
public class MockedIntegrationTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static PaymentRequest MakeRequest(string provider, string channel = "card") => new()
    {
        PaymentProvider = provider,
        PaymentChannel = channel,
        PaymentModel = PaymentModel.OneTime,
        Title = "Test order",
        Description = "Mocked integration test",
        Currency = "PLN",
        Amount = 9.99m,
        Email = "test@example.com",
        FirstName = "Jan",
        Surname = "Kowalski",
        AdditionalData = "test-ref-001",
    };

    // ══════════════════════════════════════════════════════════════════════════
    // CashBill
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildCashBillHost(ICashbillServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterCashbillProvider();
                // Override the real caller with our mock
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<CashBillProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task CashBill_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GeneratePayment(It.IsAny<Provider.CashBill.PaymentRequest>()))
              .ReturnsAsync(new PaymentStatus
              {
                  Id = "CB_TEST_001",
                  Status = "Start",
                  PaymentProviderRedirectUrl = "https://pay.cashbill.pl/test/CB_TEST_001",
              });

        var svc = BuildCashBillHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Cashbill"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("CB_TEST_001", result.PaymentUniqueId);
        Assert.NotEmpty(result.RedirectUrl!);
    }

    [Fact]
    public async Task CashBill_GetStatus_ReturnsCompleted()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetPaymentStatus("CB_TEST_001"))
              .ReturnsAsync(new PaymentStatus { Id = "CB_TEST_001", Status = "PositiveFinish" });

        var svc = BuildCashBillHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Cashbill", "CB_TEST_001");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task CashBill_GetChannels_ReturnsList()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetPaymentChannels("PLN"))
              .ReturnsAsync(new List<PaymentChannels>
              {
                  new() { Id = "blik_pbl", AvailableCurrencies = ["PLN"], Name = "BLIK" },
                  new() { Id = "card_visa", AvailableCurrencies = ["PLN"], Name = "Karta Visa" },
              });

        var svc = BuildCashBillHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var channels = await svc.GetChannels("Cashbill", "PLN");

        Assert.NotEmpty(channels);
        Assert.Contains(channels, c => c.Id == "blik_pbl");
    }

    [Fact]
    public async Task CashBill_GetStatus_Negative_ReturnsFailed()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetPaymentStatus("CB_FAIL"))
              .ReturnsAsync(new PaymentStatus { Id = "CB_FAIL", Status = "NegativeFinish" });

        var svc = BuildCashBillHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Cashbill", "CB_FAIL");

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task CashBill_TransactionStatusChange_ValidSign_ReturnsStatus()
    {
        var caller = new Mock<ICashbillServiceCaller>();
        caller.Setup(c => c.GetSignForNotificationService(It.IsAny<TransactionStatusChanged>()))
              .ReturnsAsync("expectedsign");
        caller.Setup(c => c.GetPaymentStatus("TX_001"))
              .ReturnsAsync(new PaymentStatus { Id = "TX_001", Status = "PositiveFinish" });

        var svc = BuildCashBillHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("Cashbill", new TransactionStatusChangePayload
        {
            ProviderId = "Cashbill",
            Payload = null,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "cmd",  new StringValues("transactionStatusChanged") },
                { "args", new StringValues("TX_001") },
                { "sign", new StringValues("expectedsign") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Stripe
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildStripeHost(IStripeServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterStripeProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<StripeProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task Stripe_RegisterPayment_ReturnsCreated()
    {
        var session = new global::Stripe.Checkout.Session
        {
            Id = "cs_test_001",
            Url = "https://checkout.stripe.com/pay/cs_test_001",
        };

        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(session);

        var svc = BuildStripeHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Stripe"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("cs_test_001", result.PaymentUniqueId);
        Assert.Equal("https://checkout.stripe.com/pay/cs_test_001", result.RedirectUrl);
    }

    [Fact]
    public async Task Stripe_GetStatus_CompleteSession_ReturnsFinished()
    {
        var session = new global::Stripe.Checkout.Session
        {
            Id = "cs_test_002",
            Status = "complete",
            PaymentStatus = "paid",
        };

        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.GetCheckoutSessionAsync("cs_test_002"))
              .ReturnsAsync(session);

        var svc = BuildStripeHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Stripe", "cs_test_002");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Stripe_GetStatus_OpenSession_ReturnsCreated()
    {
        var session = new global::Stripe.Checkout.Session
        {
            Id = "cs_test_003",
            Status = "open",
            PaymentStatus = "unpaid",
        };

        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.GetCheckoutSessionAsync("cs_test_003"))
              .ReturnsAsync(session);

        var svc = BuildStripeHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Stripe", "cs_test_003");

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }

    [Fact]
    public async Task Stripe_TransactionStatusChange_CheckoutCompleted_ReturnsCompleted()
    {
        var stripeEvent = new global::Stripe.Event
        {
            Type = "checkout.session.completed",
            Data = new global::Stripe.EventData
            {
                Object = new global::Stripe.Checkout.Session
                {
                    Id = "cs_test_004",
                    PaymentStatus = "paid",
                },
            },
        };

        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(stripeEvent);

        var svc = BuildStripeHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("Stripe", new TransactionStatusChangePayload
        {
            ProviderId = "Stripe",
            Payload = """{"id":"evt_test","type":"checkout.session.completed"}""",
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "Stripe-Signature", new StringValues("t=1,v1=mocksignature") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Stripe_TransactionStatusChange_PaymentFailed_ReturnsFailed()
    {
        var stripeEvent = new global::Stripe.Event
        {
            Type = "payment_intent.payment_failed",
            Data = new global::Stripe.EventData
            {
                Object = new global::Stripe.PaymentIntent { Id = "pi_fail" },
            },
        };

        var caller = new Mock<IStripeServiceCaller>();
        caller.Setup(c => c.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(stripeEvent);

        var svc = BuildStripeHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("Stripe", new TransactionStatusChangePayload
        {
            ProviderId = "Stripe",
            Payload = """{"id":"evt_fail","type":"payment_intent.payment_failed"}""",
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "Stripe-Signature", new StringValues("t=1,v1=mocksignature") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Adyen
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildAdyenHost(IAdyenServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterAdyenProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<AdyenProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task Adyen_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.CreateSessionAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(("session_adyen_001", "https://checkout.adyen.com/pay/session_adyen_001", null));

        var svc = BuildAdyenHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Adyen"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("session_adyen_001", result.PaymentUniqueId);
        Assert.NotEmpty(result.RedirectUrl!);
    }

    [Fact]
    public async Task Adyen_RegisterPayment_Error_ReturnsRejected()
    {
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.CreateSessionAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(((string?)null, (string?)null, "Unauthorized"));

        var svc = BuildAdyenHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Adyen"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task Adyen_GetStatus_ReturnsCompleted()
    {
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.GetPaymentStatusAsync("adyen_ref_001"))
              .ReturnsAsync(PaymentStatusEnum.Finished);

        var svc = BuildAdyenHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Adyen", "adyen_ref_001");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Adyen_TransactionStatusChange_ValidHmac_ReturnsStatus()
    {
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.VerifyNotificationHmac(It.IsAny<string>(), "valid_hmac"))
              .Returns(true);

        var svc = BuildAdyenHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"pspReference":"adyen_ref_002","success":"true","eventCode":"AUTHORISATION"}""";
        var result = await svc.TransactionStatusChange("Adyen", new TransactionStatusChangePayload
        {
            ProviderId = "Adyen",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "HmacSignature", new StringValues("valid_hmac") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PayU
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildPayUHost(IPayUServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterPayUProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<PayUProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task PayU_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IPayUServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tok_001");
        caller.Setup(c => c.CreateOrderAsync("tok_001", It.IsAny<PaymentRequest>()))
              .ReturnsAsync(("order_payu_001", "https://secure.payu.com/pay/order_payu_001", null));

        var svc = BuildPayUHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("PayU", "blik"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("order_payu_001", result.PaymentUniqueId);
    }

    [Fact]
    public async Task PayU_RegisterPayment_Error_ReturnsRejected()
    {
        var caller = new Mock<IPayUServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tok_002");
        caller.Setup(c => c.CreateOrderAsync("tok_002", It.IsAny<PaymentRequest>()))
              .ReturnsAsync(((string?)null, (string?)null, "Unauthorized"));

        var svc = BuildPayUHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("PayU"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task PayU_GetStatus_ReturnsCompleted()
    {
        var caller = new Mock<IPayUServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tok_003");
        caller.Setup(c => c.GetOrderStatusAsync("tok_003", "order_payu_002"))
              .ReturnsAsync(PaymentStatusEnum.Finished);

        var svc = BuildPayUHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("PayU", "order_payu_002");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task PayU_TransactionStatusChange_ValidSignature_ReturnsCompleted()
    {
        var caller = new Mock<IPayUServiceCaller>();
        caller.Setup(c => c.VerifySignature(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(true);

        var svc = BuildPayUHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"order":{"status":"COMPLETED","orderId":"order_payu_003"}}""";
        var result = await svc.TransactionStatusChange("PayU", new TransactionStatusChangePayload
        {
            ProviderId = "PayU",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "OpenPayU-Signature", new StringValues("sender=checkout;signature=valid;algorithm=MD5;content=DOCUMENT") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task PayU_TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var caller = new Mock<IPayUServiceCaller>();
        caller.Setup(c => c.VerifySignature(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(false);

        var svc = BuildPayUHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("PayU", new TransactionStatusChangePayload
        {
            ProviderId = "PayU",
            Payload = """{"order":{"status":"COMPLETED"}}""",
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "OpenPayU-Signature", new StringValues("sender=checkout;signature=bad;algorithm=MD5;content=DOCUMENT") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Przelewy24
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildPrzelewy24Host(IPrzelewy24ServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterPrzelewy24Provider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<Przelewy24Provider>();
            })
            .Build();
    }

    [Fact]
    public async Task Przelewy24_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.ComputeSign(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<string>()))
              .Returns("mock_sign");
        caller.Setup(c => c.RegisterTransactionAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
              .ReturnsAsync(("p24_token_001", null));

        var svc = BuildPrzelewy24Host(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Przelewy24", "online_transfer"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.NotEmpty(result.RedirectUrl!);
    }

    [Fact]
    public async Task Przelewy24_RegisterPayment_Error_ReturnsRejected()
    {
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.ComputeSign(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<string>()))
              .Returns("mock_sign");
        caller.Setup(c => c.RegisterTransactionAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
              .ReturnsAsync(((string?)null, "Unauthorized"));

        var svc = BuildPrzelewy24Host(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Przelewy24"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task Przelewy24_TransactionStatusChange_ValidNotification_ReturnsCompleted()
    {
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(It.IsAny<string>())).Returns(true);
        caller.Setup(c => c.ComputeSign(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<string>()))
              .Returns("mock_sign");
        caller.Setup(c => c.VerifyTransactionAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
              .ReturnsAsync(PaymentStatusEnum.Finished);

        var svc = BuildPrzelewy24Host(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"merchantId":1234,"posId":1234,"sessionId":"sess_001","amount":999,"originAmount":999,"currency":"PLN","orderId":5678,"sign":"valid_sign"}""";
        var result = await svc.TransactionStatusChange("Przelewy24", new TransactionStatusChangePayload
        {
            ProviderId = "Przelewy24",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>(),
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Przelewy24_TransactionStatusChange_InvalidNotification_ReturnsRejected()
    {
        var caller = new Mock<IPrzelewy24ServiceCaller>();
        caller.Setup(c => c.VerifyNotification(It.IsAny<string>())).Returns(false);

        var svc = BuildPrzelewy24Host(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("Przelewy24", new TransactionStatusChangePayload
        {
            ProviderId = "Przelewy24",
            Payload = """{"merchantId":0,"posId":0,"sessionId":"x","amount":0,"orderId":0,"sign":"bad"}""",
            QueryParameters = new Dictionary<string, StringValues>(),
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Tpay
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildTpayHost(ITpayServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterTpayProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<TpayProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task Tpay_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<ITpayServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tpay_tok_001");
        caller.Setup(c => c.CreateTransactionAsync("tpay_tok_001", It.IsAny<PaymentRequest>()))
              .ReturnsAsync(("tpay_tx_001", "https://secure.tpay.com/pay/tpay_tx_001"));

        var svc = BuildTpayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Tpay"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("tpay_tx_001", result.PaymentUniqueId);
        Assert.Equal("https://secure.tpay.com/pay/tpay_tx_001", result.RedirectUrl);
    }

    [Fact]
    public async Task Tpay_RegisterPayment_NullResult_ReturnsRejected()
    {
        var caller = new Mock<ITpayServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tpay_tok_002");
        caller.Setup(c => c.CreateTransactionAsync("tpay_tok_002", It.IsAny<PaymentRequest>()))
              .ReturnsAsync(((string?)null, (string?)null));

        var svc = BuildTpayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Tpay"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task Tpay_GetStatus_ReturnsCompleted()
    {
        var caller = new Mock<ITpayServiceCaller>();
        caller.Setup(c => c.GetAccessTokenAsync()).ReturnsAsync("tpay_tok_003");
        caller.Setup(c => c.GetTransactionStatusAsync("tpay_tok_003", "tpay_tx_002"))
              .ReturnsAsync(PaymentStatusEnum.Finished);

        var svc = BuildTpayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Tpay", "tpay_tx_002");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Tpay_TransactionStatusChange_ValidSignature_ReturnsCompleted()
    {
        var caller = new Mock<ITpayServiceCaller>();
        caller.Setup(c => c.VerifyNotification(It.IsAny<string>(), "valid_sig"))
              .Returns(true);

        var svc = BuildTpayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"transactionId":"tpay_tx_003","status":"correct"}""";
        var result = await svc.TransactionStatusChange("Tpay", new TransactionStatusChangePayload
        {
            ProviderId = "Tpay",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "X-Signature", new StringValues("valid_sig") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HotPay
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildHotPayHost(IHotPayServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterHotPayProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<HotPayProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task HotPay_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IHotPayServiceCaller>();
        caller.Setup(c => c.InitPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
              .ReturnsAsync(("hotpay_001", "https://pay.hotpay.pl/hotpay_001"));

        var svc = BuildHotPayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("HotPay"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.NotEmpty(result.PaymentUniqueId!);
        Assert.Equal("https://pay.hotpay.pl/hotpay_001", result.RedirectUrl);
    }

    [Fact]
    public async Task HotPay_RegisterPayment_NullResult_ReturnsRejected()
    {
        var caller = new Mock<IHotPayServiceCaller>();
        caller.Setup(c => c.InitPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<string>()))
              .ReturnsAsync(((string?)null, (string?)null));

        var svc = BuildHotPayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("HotPay"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task HotPay_TransactionStatusChange_ValidHash_ReturnsCompleted()
    {
        var caller = new Mock<IHotPayServiceCaller>();
        caller.Setup(c => c.VerifyNotification("valid_hash", "9.99", "hotpay_001", "SUCCESS"))
              .Returns(true);

        var svc = BuildHotPayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("HotPay", new TransactionStatusChangePayload
        {
            ProviderId = "HotPay",
            Payload = string.Empty,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "HASH",         new StringValues("valid_hash") },
                { "KWOTA",        new StringValues("9.99") },
                { "ID_PLATNOSCI", new StringValues("hotpay_001") },
                { "STATUS",       new StringValues("SUCCESS") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task HotPay_TransactionStatusChange_ValidHash_Failure_ReturnsFailed()
    {
        var caller = new Mock<IHotPayServiceCaller>();
        caller.Setup(c => c.VerifyNotification("valid_hash_fail", "9.99", "hotpay_002", "FAILURE"))
              .Returns(true);

        var svc = BuildHotPayHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.TransactionStatusChange("HotPay", new TransactionStatusChangePayload
        {
            ProviderId = "HotPay",
            Payload = string.Empty,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "HASH",         new StringValues("valid_hash_fail") },
                { "KWOTA",        new StringValues("9.99") },
                { "ID_PLATNOSCI", new StringValues("hotpay_002") },
                { "STATUS",       new StringValues("FAILURE") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PayNow
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildPayNowHost(IPayNowServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterPayNowProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<PayNowProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task PayNow_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IPayNowServiceCaller>();
        caller.Setup(c => c.CreatePaymentAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(("paynow_001", "https://app.paynow.pl/pay/paynow_001"));

        var svc = BuildPayNowHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("PayNow", "BLIK"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("paynow_001", result.PaymentUniqueId);
        Assert.Equal("https://app.paynow.pl/pay/paynow_001", result.RedirectUrl);
    }

    [Fact]
    public async Task PayNow_RegisterPayment_NullResult_ReturnsRejected()
    {
        var caller = new Mock<IPayNowServiceCaller>();
        caller.Setup(c => c.CreatePaymentAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(((string?)null, (string?)null));

        var svc = BuildPayNowHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("PayNow"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task PayNow_GetStatus_ReturnsCompleted()
    {
        var caller = new Mock<IPayNowServiceCaller>();
        caller.Setup(c => c.GetPaymentStatusAsync("paynow_002"))
              .ReturnsAsync(PaymentStatusEnum.Finished);

        var svc = BuildPayNowHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("PayNow", "paynow_002");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task PayNow_TransactionStatusChange_ValidSignature_ReturnsCompleted()
    {
        var caller = new Mock<IPayNowServiceCaller>();
        caller.Setup(c => c.VerifySignature(It.IsAny<string>(), "valid_sig"))
              .Returns(true);

        var svc = BuildPayNowHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"paymentId":"paynow_003","status":"CONFIRMED"}""";
        var result = await svc.TransactionStatusChange("PayNow", new TransactionStatusChangePayload
        {
            ProviderId = "PayNow",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "Signature", new StringValues("valid_sig") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Revolut
    // ══════════════════════════════════════════════════════════════════════════

    private static IHost BuildRevolutHost(IRevolutServiceCaller caller)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.RegisterRevolutProvider();
                services.AddTransient(_ => caller);
                services.AddPayments()
                    .RegisterPaymentProvider<RevolutProvider>();
            })
            .Build();
    }

    [Fact]
    public async Task Revolut_RegisterPayment_ReturnsCreated()
    {
        var caller = new Mock<IRevolutServiceCaller>();
        caller.Setup(c => c.CreateOrderAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(("rev_order_001", "https://checkout.revolut.com/pay/rev_order_001"));

        var svc = BuildRevolutHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Revolut", "card"));

        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.Equal("rev_order_001", result.PaymentUniqueId);
        Assert.Equal("https://checkout.revolut.com/pay/rev_order_001", result.RedirectUrl);
    }

    [Fact]
    public async Task Revolut_RegisterPayment_NullResult_ReturnsRejected()
    {
        var caller = new Mock<IRevolutServiceCaller>();
        caller.Setup(c => c.CreateOrderAsync(It.IsAny<PaymentRequest>()))
              .ReturnsAsync(((string?)null, (string?)null));

        var svc = BuildRevolutHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.RegisterPayment(MakeRequest("Revolut"));

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task Revolut_GetStatus_Completed_ReturnsCompleted()
    {
        var caller = new Mock<IRevolutServiceCaller>();
        caller.Setup(c => c.GetOrderAsync("rev_order_002"))
              .ReturnsAsync(("completed", "rev_order_002"));

        var svc = BuildRevolutHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Revolut", "rev_order_002");

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }

    [Fact]
    public async Task Revolut_GetStatus_Pending_ReturnsProcessing()
    {
        var caller = new Mock<IRevolutServiceCaller>();
        caller.Setup(c => c.GetOrderAsync("rev_order_003"))
              .ReturnsAsync(("pending", "rev_order_003"));

        var svc = BuildRevolutHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var result = await svc.GetStatus("Revolut", "rev_order_003");

        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task Revolut_TransactionStatusChange_ValidSignature_ReturnsCompleted()
    {
        var caller = new Mock<IRevolutServiceCaller>();
        caller.Setup(c => c.VerifyWebhookSignature(It.IsAny<string>(), "1711000000", "v1=valid_sig"))
              .Returns(true);

        var svc = BuildRevolutHost(caller.Object).Services.GetRequiredService<IPaymentService>();
        var body = """{"order_id":"rev_order_004","event":"ORDER_COMPLETED"}""";
        var result = await svc.TransactionStatusChange("Revolut", new TransactionStatusChangePayload
        {
            ProviderId = "Revolut",
            Payload = body,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "Revolut-Request-Timestamp", new StringValues("1711000000") },
                { "Revolut-Signature",         new StringValues("v1=valid_sig") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Finished, result.PaymentStatus);
    }
}
