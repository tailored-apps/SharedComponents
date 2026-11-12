using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.Tpay;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// 3.0 removed the obsolete ApiUrl alias of PayNow and Tpay. A configuration that still sets ApiUrl would
/// otherwise be ignored and the provider would call the production default of ServiceUrl - for a sandbox
/// setup a silent switch to production. The host refuses to start instead.
/// </summary>
public class RetiredOptionKeyTests
{
    private static IConfiguration Configuration(string provider, string key, string url) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            [$"Payments:Providers:{provider}:{key}"] = url,
            // the settings both providers require, so that only the URL key decides the outcome
            ["Payments:Providers:PayNow:ApiKey"] = "key",
            ["Payments:Providers:PayNow:SignatureKey"] = "signature",
            ["Payments:Providers:Tpay:ClientId"] = "client",
            ["Payments:Providers:Tpay:ClientSecret"] = "secret",
            ["Payments:Providers:Tpay:SecurityCode"] = "code",
        })
        .Build();

    [Fact]
    public void When_Tpay_Configuration_Still_Sets_ApiUrl_Should_Throw_Naming_The_Replacement()
    {
        var configure = new TpayConfigureOptions(Configuration("Tpay", "ApiUrl", "https://openapi.sandbox.tpay.com"));

        var exception = Assert.Throws<InvalidOperationException>(() => configure.Configure(new TpayServiceOptions()));

        Assert.Equal("'Payments:Providers:Tpay:ApiUrl' is no longer read (removed in 3.0): rename it to 'ServiceUrl'.", exception.Message);
    }

    [Fact]
    public void When_PayNow_Configuration_Still_Sets_ApiUrl_Should_Throw_Naming_The_Replacement()
    {
        var configure = new PayNowConfigureOptions(Configuration("PayNow", "ApiUrl", "https://api.sandbox.paynow.pl"));

        var exception = Assert.Throws<InvalidOperationException>(() => configure.Configure(new PayNowServiceOptions()));

        Assert.Equal("'Payments:Providers:PayNow:ApiUrl' is no longer read (removed in 3.0): rename it to 'ServiceUrl'.", exception.Message);
    }

    [Theory]
    [InlineData("Tpay")]
    [InlineData("PayNow")]
    public async Task When_The_Retired_Key_Is_Configured_Should_Stop_The_Host_From_Starting(string provider)
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(Configuration(provider, "ApiUrl", "https://sandbox.example")))
            .ConfigureServices(services =>
            {
                var payments = services.AddPayments();
                if (provider == "Tpay")
                    payments.RegisterTpayProvider();
                else
                    payments.RegisterPayNowProvider();
            })
            .Build();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Contains($"'Payments:Providers:{provider}:ApiUrl' is no longer read", exception.ToString());
    }

    [Fact]
    public void When_ServiceUrl_Is_Configured_Should_Use_It()
    {
        var tpay = new TpayServiceOptions();
        new TpayConfigureOptions(Configuration("Tpay", "ServiceUrl", "https://openapi.sandbox.tpay.com")).Configure(tpay);
        var payNow = new PayNowServiceOptions();
        new PayNowConfigureOptions(Configuration("PayNow", "ServiceUrl", "https://api.sandbox.paynow.pl")).Configure(payNow);

        Assert.Equal("https://openapi.sandbox.tpay.com", tpay.ServiceUrl);
        Assert.Equal("https://api.sandbox.paynow.pl", payNow.ServiceUrl);
    }

    [Theory]
    [InlineData(typeof(TpayServiceOptions))]
    [InlineData(typeof(PayNowServiceOptions))]
    public void When_The_Options_Are_Inspected_Should_Have_No_ApiUrl_Alias(Type options)
    {
        Assert.Null(options.GetProperty("ApiUrl"));
        Assert.NotNull(options.GetProperty("ServiceUrl"));
    }
}
