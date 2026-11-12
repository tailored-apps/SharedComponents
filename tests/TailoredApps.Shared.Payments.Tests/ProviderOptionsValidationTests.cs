#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.CashBill;
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
/// A registered provider without its credentials or webhook secret must be reported when the host
/// starts, naming every missing setting - not on the first payment.
/// </summary>
public class ProviderOptionsValidationTests
{
    private static IOptions<TOptions> Resolve<TOptions>(Action<IServiceCollection> register, Dictionary<string, string?> settings)
        where TOptions : class
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        register(services);
        return services.BuildServiceProvider().GetRequiredService<IOptions<TOptions>>();
    }

    private static void AssertRequires<TOptions>(Action<IServiceCollection> register, string section, params string[] required)
        where TOptions : class
    {
        // Nothing configured: every required setting is named.
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve<TOptions>(register, new()).Value);
        foreach (var setting in required)
        {
            Assert.Contains(exception.Failures, failure => failure.Contains($"'{section}:{setting}'"));
        }

        Assert.Equal(required.Length, exception.Failures.Count());

        // One setting still missing: only that one is reported.
        foreach (var missing in required)
        {
            var partial = required.Where(s => s != missing).ToDictionary(s => $"{section}:{s}", _ => (string?)"1");
            var single = Assert.Throws<OptionsValidationException>(() => Resolve<TOptions>(register, partial).Value);
            Assert.Equal($"'{section}:{missing}' is not configured.", Assert.Single(single.Failures));
        }

        // Everything required is present: the options resolve.
        var complete = required.ToDictionary(s => $"{section}:{s}", _ => (string?)"1");
        Assert.NotNull(Resolve<TOptions>(register, complete).Value);
    }

    [Fact]
    public void When_Adyen_Is_Registered_Should_Require_Credentials_And_Notification_Key()
        => AssertRequires<AdyenServiceOptions>(s => s.RegisterAdyenProvider(), "Payments:Providers:Adyen", "ApiKey", "MerchantAccount", "NotificationHmacKey");

    [Fact]
    public void When_CashBill_Is_Registered_Should_Require_Service_Url_Shop_And_Secret()
        => AssertRequires<CashbillServiceOptions>(s => s.RegisterCashbillProvider(), "Payments:Providers:Cashbill", "ServiceUrl", "ShopId", "ShopSecretPhrase");

    [Fact]
    public void When_HotPay_Is_Registered_Should_Require_The_Secret_Hash()
        => AssertRequires<HotPayServiceOptions>(s => s.RegisterHotPayProvider(), "Payments:Providers:HotPay", "SecretHash");

    [Fact]
    public void When_PayNow_Is_Registered_Should_Require_Api_Key_And_Signature_Key()
        => AssertRequires<PayNowServiceOptions>(s => s.RegisterPayNowProvider(), "Payments:Providers:PayNow", "ApiKey", "SignatureKey");

    [Fact]
    public void When_PayU_Is_Registered_Should_Require_Client_Pos_And_Signature_Key()
        => AssertRequires<PayUServiceOptions>(s => s.RegisterPayUProvider(), "Payments:Providers:PayU", "ClientId", "ClientSecret", "PosId", "SignatureKey");

    [Fact]
    public void When_Przelewy24_Is_Registered_Should_Require_Merchant_Pos_Api_Key_And_Crc_Key()
        => AssertRequires<Przelewy24ServiceOptions>(s => s.RegisterPrzelewy24Provider(), "Payments:Providers:Przelewy24", "MerchantId", "PosId", "ApiKey", "CrcKey");

    [Fact]
    public void When_Revolut_Is_Registered_Should_Require_Api_Key_And_Webhook_Secret()
        => AssertRequires<RevolutServiceOptions>(s => s.RegisterRevolutProvider(), "Payments:Providers:Revolut", "ApiKey", "WebhookSecret");

    [Fact]
    public void When_Stripe_Is_Registered_Should_Require_Secret_Key_And_Webhook_Secret()
        => AssertRequires<StripeServiceOptions>(s => s.RegisterStripeProvider(), "Payments:Providers:Stripe", "SecretKey", "WebhookSecret");

    [Fact]
    public void When_Tpay_Is_Registered_Should_Require_Client_Credentials_And_Security_Code()
        => AssertRequires<TpayServiceOptions>(s => s.RegisterTpayProvider(), "Payments:Providers:Tpay", "ClientId", "ClientSecret", "SecurityCode");

    [Fact]
    public async Task When_A_Host_Starts_With_An_Unconfigured_Provider_Should_Fail_At_Startup()
    {
        // HostBuilder without default configuration sources: nothing is configured.
        using var host = new HostBuilder()
            .ConfigureServices(services => services.RegisterHotPayProvider())
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("'Payments:Providers:HotPay:SecretHash' is not configured.", exception.Failures);
    }

    [Fact]
    public async Task When_A_Host_Starts_With_A_Configured_Provider_Should_Start()
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Payments:Providers:HotPay:SecretHash"] = "secret" }))
            .ConfigureServices(services => services.RegisterHotPayProvider())
            .Build();

        await host.StartAsync();
        await host.StopAsync();
    }
}
