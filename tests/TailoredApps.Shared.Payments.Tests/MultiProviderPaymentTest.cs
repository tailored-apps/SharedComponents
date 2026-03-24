using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
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

/// <summary>
/// Smoke testy dla 7 nowych providerów płatności.
/// Testy jednostkowe — nie wymagają połączenia sieciowego.
/// </summary>
public class MultiProviderPaymentTest
{
    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddJsonFile("appsettings.json", optional: true))
            .ConfigureServices((_, services) =>
            {
                services.RegisterAdyenProvider();
                services.RegisterPayUProvider();
                services.RegisterPrzelewy24Provider();
                services.RegisterTpayProvider();
                services.RegisterHotPayProvider();
                services.RegisterPayNowProvider();
                services.RegisterRevolutProvider();

                services.AddPayments()
                    .RegisterPaymentProvider<AdyenProvider>()
                    .RegisterPaymentProvider<PayUProvider>()
                    .RegisterPaymentProvider<Przelewy24Provider>()
                    .RegisterPaymentProvider<TpayProvider>()
                    .RegisterPaymentProvider<HotPayProvider>()
                    .RegisterPaymentProvider<PayNowProvider>()
                    .RegisterPaymentProvider<RevolutProvider>();
            })
            .Build();

    // ─── Provider metadata ────────────────────────────────────────────────────

    [Fact]
    public async Task AllProviders_AreRegistered()
    {
        var host      = BuildHost();
        var service   = host.Services.GetRequiredService<IPaymentService>();
        var providers = await service.GetProviders();
        var ids       = providers.Select(p => p.Id).ToList();

        Assert.Contains("Adyen",      ids);
        Assert.Contains("PayU",       ids);
        Assert.Contains("Przelewy24", ids);
        Assert.Contains("Tpay",       ids);
        Assert.Contains("HotPay",     ids);
        Assert.Contains("PayNow",     ids);
        Assert.Contains("Revolut",    ids);
    }

    // ─── GetChannels ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Adyen",      "PLN")]
    [InlineData("PayU",       "PLN")]
    [InlineData("Przelewy24", "PLN")]
    [InlineData("Tpay",       "PLN")]
    [InlineData("HotPay",     "PLN")]
    [InlineData("PayNow",     "PLN")]
    [InlineData("Revolut",    "PLN")]
    public async Task GetChannels_PLN_ReturnsNonEmptyList(string providerKey, string currency)
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels(providerKey, currency);
        Assert.NotEmpty(channels);
    }

    [Fact]
    public async Task Adyen_GetChannels_EUR_ContainsIdeal()
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels("Adyen", "EUR");
        Assert.Contains(channels, c => c.Id == "ideal");
    }

    [Fact]
    public async Task PayU_GetChannels_PLN_ContainsBlik()
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels("PayU", "PLN");
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task Revolut_GetChannels_ContainsRevolutPay()
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels("Revolut", "PLN");
        Assert.Contains(channels, c => c.Id == "revolut_pay");
    }

    [Fact]
    public async Task PayNow_GetChannels_ContainsBlikAndCard()
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels("PayNow", "PLN");
        Assert.Contains(channels, c => c.Id == "BLIK");
        Assert.Contains(channels, c => c.Id == "CARD");
    }

    [Fact]
    public async Task Przelewy24_GetChannels_ContainsOnlineTransfer()
    {
        var host     = BuildHost();
        var service  = host.Services.GetRequiredService<IPaymentService>();
        var channels = await service.GetChannels("Przelewy24", "PLN");
        Assert.Contains(channels, c => c.Id == "online_transfer");
    }

    // ─── Provider info ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Adyen",      "https://www.adyen.com")]
    [InlineData("PayU",       "https://payu.pl")]
    [InlineData("Przelewy24", "https://przelewy24.pl")]
    [InlineData("Tpay",       "https://tpay.com")]
    [InlineData("HotPay",     "https://hotpay.pl")]
    [InlineData("PayNow",     "https://paynow.pl")]
    [InlineData("Revolut",    "https://revolut.com/business")]
    public async Task Provider_HasCorrectUrl(string providerKey, string expectedUrl)
    {
        var host      = BuildHost();
        var service   = host.Services.GetRequiredService<IPaymentService>();
        var providers = await service.GetProviders();
        var provider  = providers.Single(p => p.Id == providerKey);
        Assert.Equal(expectedUrl, provider.Url);
    }

    // ─── Invalid webhook → Rejected ──────────────────────────────────────────

    [Theory]
    [InlineData("PayU")]
    [InlineData("Przelewy24")]
    [InlineData("Tpay")]
    [InlineData("PayNow")]
    [InlineData("Revolut")]
    [InlineData("Adyen")]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected(string providerKey)
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();

        var result = await service.TransactionStatusChange(providerKey, new TransactionStatusChangePayload
        {
            ProviderId = providerKey,
            Payload    = """{"status":"CONFIRMED","orderId":"test_123"}""",
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "OpenPayU-Signature",        new StringValues("sender=checkout;signature=invalid;algorithm=MD5;content=DOCUMENT") },
                { "Signature",                 new StringValues("invalidsignature") },
                { "X-Signature",               new StringValues("invalidsignature") },
                { "HmacSignature",             new StringValues("invalidsignature") },
                { "Revolut-Signature",         new StringValues("v1=invalidsignature") },
                { "Revolut-Request-Timestamp", new StringValues("1234567890") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    [Fact]
    public async Task HotPay_TransactionStatusChange_InvalidHash_ReturnsRejected()
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();

        var result = await service.TransactionStatusChange("HotPay", new TransactionStatusChangePayload
        {
            ProviderId = "HotPay",
            Payload    = string.Empty,
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "HASH",         new StringValues("invalidhash") },
                { "KWOTA",        new StringValues("9.99") },
                { "ID_PLATNOSCI", new StringValues("test_123") },
                { "STATUS",       new StringValues("SUCCESS") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ─── GetStatus (offline) ──────────────────────────────────────────────────

    [Fact]
    public async Task HotPay_GetStatus_ReturnsProcessing()
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();
        var result  = await service.GetStatus("HotPay", "test-payment-id");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    [Fact]
    public async Task Przelewy24_GetStatus_ReturnsProcessing()
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();
        var result  = await service.GetStatus("Przelewy24", "test-session-id");
        Assert.Equal(PaymentStatusEnum.Processing, result.PaymentStatus);
    }

    // ─── Integration tests (Skip by default) ─────────────────────────────────

    [Fact(Skip = "Integration — requires real PayU sandbox credentials in appsettings.json")]
    public async Task PayU_RequestPayment_CreatesOrder()
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();
        var result  = await service.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "PayU",
            PaymentChannel  = "c",
            PaymentModel    = PaymentModel.OneTime,
            Title           = "Test order",
            Currency        = "PLN",
            Amount          = 1.00m,
            Email           = "test@example.com",
        });
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
        Assert.NotEmpty(result.RedirectUrl!);
    }

    [Fact(Skip = "Integration — requires real Revolut sandbox credentials in appsettings.json")]
    public async Task Revolut_RequestPayment_CreatesOrder()
    {
        var host    = BuildHost();
        var service = host.Services.GetRequiredService<IPaymentService>();
        var result  = await service.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "Revolut",
            PaymentChannel  = "card",
            PaymentModel    = PaymentModel.OneTime,
            Title           = "Test order",
            Currency        = "PLN",
            Amount          = 1.00m,
            Email           = "test@example.com",
        });
        Assert.Equal(PaymentStatusEnum.Created, result.PaymentStatus);
    }
}
