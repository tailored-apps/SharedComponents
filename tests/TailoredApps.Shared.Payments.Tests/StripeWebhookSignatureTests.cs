using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using global::Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.Payments.Provider.Stripe;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Testy weryfikacji podpisu (Stripe-Signature) w webhookach Stripe.
///
/// Stripe używa HMAC-SHA256 z webhookSecret jako kluczem.
/// Format nagłówka: t={unix_timestamp},v1={hmac_hex}
/// Treść do podpisania: "{t}.{rawPayload}"
///
/// Docs: https://stripe.com/docs/webhooks/signatures
/// </summary>
public class StripeWebhookSignatureTests
{
    // ─── Test webhook secret (lokalny, nie produkcyjny) ──────────────────────
    // Stripe SDK obcina prefix "whsec_" automatycznie w EventUtility.
    // HMAC klucz = reszta po "whsec_".
    private const string TestWebhookSecret = "whsec_test1234567890abcdef1234567890abcdef";

    // Przykładowy payload checkout.session.completed zgodny ze strukturą Stripe Event.
    // Stripe EventConverter wymaga pola "object" jako discriminatora typu w data.object.
    private const string SamplePayload = """{"id":"evt_test_001","object":"event","api_version":"2024-06-20","created":1711234567,"livemode":false,"pending_webhooks":1,"request":{"id":null,"idempotency_key":null},"type":"checkout.session.completed","data":{"object":{"id":"cs_test_abc123","object":"checkout.session","payment_status":"paid","status":"complete","livemode":false,"amount_total":999,"currency":"pln","customer_email":"test@example.com"}}}""";

    /// <summary>
    /// Oblicza poprawny nagłówek Stripe-Signature.
    /// Stripe SDK: klucz HMAC = secret BEZ prefixu "whsec_".
    /// Format: t={unix_ts},v1={hmac_hex}
    /// Signed payload: "{t}.{rawPayload}" (HMAC-SHA256).
    /// </summary>
    public static string ComputeStripeSignature(string payload, string secret, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";
        // Stripe SDK (EventUtility.ValidateSignature) używa pełnego sekretu jako klucza HMAC,
        // włącznie z prefixem "whsec_" — musimy robić dokładnie to samo.
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        var signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"t={ts},v1={signature}";
    }

    /// <summary>
    /// ConstructWebhookEvent akceptuje poprawny podpis HMAC-SHA256.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_ValidSignature_ReturnsEvent()
    {
        var caller = BuildCallerWithSecret(TestWebhookSecret);

        // Stripe SDK tolerancja domyślna = 300s → timestamp musi być świeży
        var signature = ComputeStripeSignature(SamplePayload, TestWebhookSecret);

        var stripeEvent = caller.ConstructWebhookEvent(SamplePayload, signature);

        Assert.NotNull(stripeEvent);
        Assert.Equal("checkout.session.completed", stripeEvent.Type);
    }

    /// <summary>
    /// ConstructWebhookEvent rzuca StripeException gdy podpis jest nieprawidłowy.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_InvalidSignature_ThrowsStripeException()
    {
        var wrongSecret = "whsec_wrongsecret0000000000000000000";
        var badSignature = ComputeStripeSignature(SamplePayload, wrongSecret);

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        Assert.Throws<StripeException>(() =>
            caller.ConstructWebhookEvent(SamplePayload, badSignature));
    }

    /// <summary>
    /// ConstructWebhookEvent rzuca StripeException gdy timestamp jest zbyt stary (>300s).
    /// Chroni przed replay attacks.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_StaleTimestamp_ThrowsStripeException()
    {
        var staleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(SamplePayload, TestWebhookSecret, staleTimestamp);

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        Assert.Throws<StripeException>(() =>
            caller.ConstructWebhookEvent(SamplePayload, signature));
    }

    /// <summary>
    /// ConstructWebhookEvent rzuca StripeException gdy payload został zmodyfikowany.
    /// Integralność body jest chroniona przez HMAC.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_TamperedPayload_ThrowsStripeException()
    {
        var signature = ComputeStripeSignature(SamplePayload, TestWebhookSecret);
        var tamperedPayload = SamplePayload.Replace("paid", "unpaid");

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        Assert.Throws<StripeException>(() =>
            caller.ConstructWebhookEvent(tamperedPayload, signature));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IStripeServiceCaller BuildCallerWithSecret(string webhookSecret)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    ["Payments:Providers:Stripe:SecretKey"] = "sk_test_unused",
                    ["Payments:Providers:Stripe:WebhookSecret"] = webhookSecret,
                    ["Payments:Providers:Stripe:SuccessUrl"] = "https://example.com/ok",
                    ["Payments:Providers:Stripe:CancelUrl"] = "https://example.com/cancel",
                }!))
            .ConfigureServices((_, services) =>
            {
                services.RegisterStripeProvider();
                services.AddPayments().RegisterPaymentProvider<StripeProvider>();
            })
            .Build();

        return host.Services.GetRequiredService<IStripeServiceCaller>();
    }
}
