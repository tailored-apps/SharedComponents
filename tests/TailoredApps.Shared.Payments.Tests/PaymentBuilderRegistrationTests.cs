using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
/// <c>builder.Services.AddPayments().Register&lt;X&gt;Provider()</c>: one call registers a provider with its
/// dependencies and makes it reachable through <see cref="IPaymentService"/>.
/// </summary>
public class PaymentBuilderRegistrationTests
{
    public static IEnumerable<object[]> Providers()
    {
        yield return [typeof(AdyenProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterAdyenProvider())];
        yield return [typeof(CashBillProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterCashbillProvider())];
        yield return [typeof(HotPayProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterHotPayProvider())];
        yield return [typeof(PayNowProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterPayNowProvider())];
        yield return [typeof(PayUProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterPayUProvider())];
        yield return [typeof(Przelewy24Provider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterPrzelewy24Provider())];
        yield return [typeof(RevolutProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterRevolutProvider())];
        yield return [typeof(StripeProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterStripeProvider())];
        yield return [typeof(TpayProvider), (Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder>)(b => b.RegisterTpayProvider())];
    }

    private static int PaymentProviderRegistrations(IServiceCollection services, Type provider)
        => services.Count(d => d.ServiceType == typeof(IPaymentProvider) && d.ImplementationType == provider);

    [Theory]
    [MemberData(nameof(Providers))]
    public void When_Provider_Is_Registered_On_The_Builder_Should_Add_It_To_The_Payment_Service_And_Return_The_Builder(
        Type provider, Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder> register)
    {
        // arrange
        var services = new ServiceCollection();
        var builder = services.AddPayments();

        // act
        var returned = register(builder);

        // assert
        Assert.Same(builder, returned);
        Assert.Equal(1, PaymentProviderRegistrations(services, provider));
        Assert.Contains(services, d => d.ServiceType == provider);                           // the provider's own registration
        Assert.Contains(services, d => d.ServiceType == typeof(IWebhookPaymentProvider));     // and its webhook handling
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void When_Provider_Is_Registered_Twice_Should_Reach_The_Payment_Service_Once(
        Type provider, Func<IPaymentOptionsBuilder, IPaymentOptionsBuilder> register)
    {
        var services = new ServiceCollection();
        var builder = services.AddPayments();

        register(register(builder));

        Assert.Equal(1, PaymentProviderRegistrations(services, provider));
    }

    [Fact]
    public void When_Host_Already_Added_The_Provider_By_Hand_Should_Not_Add_It_Again()
    {
        // arrange: the two-step registration that was the only way before
        var services = new ServiceCollection();
        services.RegisterTpayProvider();
        var builder = services.AddPayments().RegisterPaymentProvider<TpayProvider>();

        // act
        builder.RegisterTpayProvider();

        // assert
        Assert.Equal(1, PaymentProviderRegistrations(services, typeof(TpayProvider)));
    }

    [Fact]
    public async Task When_Providers_Are_Chained_Should_All_Be_Reachable_Through_The_Payment_Service()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Payments:Providers:Tpay:ClientId"] = "client",
            ["Payments:Providers:Tpay:ClientSecret"] = "secret",
            ["Payments:Providers:Tpay:SecurityCode"] = "code",
            ["Payments:Providers:HotPay:SecretHash"] = "hash",
        }).Build());

        // act
        services.AddPayments().RegisterTpayProvider().RegisterHotPayProvider();
        using var provider = services.BuildServiceProvider();
        var registered = await provider.GetRequiredService<IPaymentService>().GetProviders();

        // assert
        Assert.Equal(new[] { "HotPay", "Tpay" }, registered.Select(p => p.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void When_Builder_Is_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IPaymentOptionsBuilder)null).RegisterTpayProvider());
        Assert.Throws<ArgumentNullException>(() => ((IPaymentOptionsBuilder)null).RegisterStripeProvider());
    }
}
