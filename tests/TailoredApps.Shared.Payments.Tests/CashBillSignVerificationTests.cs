using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Signature (sign) verification tests for CashBill back-channel notifications.
///
/// CashBill signs notifications with MD5(cmd + args + shopSecretPhrase), not SHA1.
/// The fixture below was produced by the CashBill *test* environment for the sandbox shop
/// configured in <c>appsettings.json</c>. No production credentials belong in this repository:
/// the secret is read from configuration only, never duplicated in source.
/// </summary>
public class CashBillSignVerificationTests
{
    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(a => a.AddJsonFile("appsettings.json"))
            .ConfigureServices((_, services) =>
            {
                services.RegisterCashbillProvider();
                services.AddPayments().RegisterPaymentProvider<CashBillProvider>();
            })
            .Build();

    private static string TestSecret(IHost host) =>
        host.Services.GetRequiredService<IConfiguration>()[$"{CashbillServiceOptions.ConfigurationKey}:ShopSecretPhrase"]
        ?? throw new InvalidOperationException("Test configuration is missing the CashBill ShopSecretPhrase.");

    /// <summary>cmd, transactionId, sign (MD5 digest produced by the CashBill test environment).</summary>
    public static IEnumerable<object[]> TestSecretData =>
    [
        ["transactionStatusChanged", "TEST_6f7zsddbw", "2050dc9f7149ef52d07f621d7d0d41b6"],
    ];

    /// <summary>
    /// Proves that GetSignForNotificationService computes MD5 (not SHA1) and matches a real
    /// notification signature produced by the CashBill test environment.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSecretData))]
    public async Task GetSignForNotificationService_ShouldReturnMd5_MatchesKnownCashBillWebhookSign(
        string cmd, string transactionId, string expectedSign)
    {
        using var host = BuildHost();
        var caller = host.Services.GetRequiredService<ICashbillServiceCaller>();

        var request = new TransactionStatusChanged
        {
            Command = cmd,
            TransactionId = transactionId,
            Sign = expectedSign,
        };

        var computedSign = await caller.GetSignForNotificationService(request);

        Assert.Equal(expectedSign, computedSign);
    }

    /// <summary>
    /// Documents the historical bug: SHA1 does NOT match what CashBill sends; MD5 does.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSecretData))]
    public void GetSignForNotificationService_SHA1WouldFail_MustBeMD5(
        string cmd, string transactionId, string cashBillSign)
    {
        using var host = BuildHost();
        var input = cmd + transactionId + TestSecret(host);

        var sha1Hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        var md5Hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

        Assert.NotEqual(cashBillSign, sha1Hash);
        Assert.Equal(cashBillSign, md5Hash);
    }
}
