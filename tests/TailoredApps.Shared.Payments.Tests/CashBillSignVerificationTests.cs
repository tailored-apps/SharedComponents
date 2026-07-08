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
/// Testy weryfikacji podpisu (sign) w notyfikacjach CashBill backchannel.
///
/// BUG: GetSignForNotificationService używa SHA1, ale CashBill wysyła MD5.
/// Formuła: MD5(cmd + args + shopSecretPhrase)
///
/// Weryfikacja empiryczna na podstawie rzeczywistych danych z zalejpajaca.pl
/// (webhook logs 2026-03-20).
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

    // --- dane z rzeczywistych webhook logów zalejpajaca.pl (2026-03-20) ---
    // Formuła CashBill: MD5(cmd + args + shopSecretPhrase)
    // Secret zalejpajaca.pl = 56d10c388c87f31d92023f6e7717ea18
    public static IEnumerable<object[]> RealWebhookData =>
    [
        // cmd, transactionId, sign (MD5 z sekretem zalejpajaca.pl)
        ["transactionStatusChanged", "TEST_b3p7espa", "803ce7dd61a8f7bee235259a54a44867"],
        ["transactionStatusChanged", "TEST_b3p7estn", "88eb946900ea479c9f051790b6220dd6"],
    ];

    // --- dane z appsettings testowych (secret = 1c5dd47b1dd0e11ffc3e2b1595c3dd67) ---
    public static IEnumerable<object[]> TestSecretData =>
    [
        // ten sam format — wygenerowany przez CashBill test env
        ["transactionStatusChanged", "TEST_6f7zsddbw", "2050dc9f7149ef52d07f621d7d0d41b6"],
    ];

    /// <summary>
    /// Dowodzi że GetSignForNotificationService oblicza MD5 (a nie SHA1).
    /// Test powinien przejść PO naprawie bugu w CashbillServiceCaller.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSecretData))]
    public async Task GetSignForNotificationService_ShouldReturnMd5_MatchesKnownCashBillWebhookSign(
        string cmd, string transactionId, string expectedSign)
    {
        var host = BuildHost();
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
    /// Dokumentuje buga: SHA1 NIE pasuje do sign wysyłanego przez CashBill.
    /// Poprawny algorytm to MD5.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSecretData))]
    public async Task GetSignForNotificationService_SHA1WouldFail_MustBeMD5(
        string cmd, string transactionId, string cashBillSign)
    {
        // Oblicz SHA1 ręcznie (stara/błędna implementacja)
        var input = cmd + transactionId + "1c5dd47b1dd0e11ffc3e2b1595c3dd67"; // test secret
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var sha1Bytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        var sha1Hash = BitConverter.ToString(sha1Bytes).Replace("-", "").ToLower();

        // Oblicz MD5 (poprawna implementacja)
        using var md5 = System.Security.Cryptography.MD5.Create();
        var md5Bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        var md5Hash = BitConverter.ToString(md5Bytes).Replace("-", "").ToLower();

        // SHA1 NIE pasuje do tego co CashBill wysyła
        Assert.NotEqual(cashBillSign, sha1Hash);

        // MD5 pasuje
        Assert.Equal(cashBillSign, md5Hash);
    }
}
