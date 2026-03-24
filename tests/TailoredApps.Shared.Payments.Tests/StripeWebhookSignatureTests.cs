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
    // W testach jednostkowych generujemy podpis ręcznie, żeby nie potrzebować
    // połączenia z Stripe CLI ani rzeczywistym kontem.
    private const string TestWebhookSecret = "whsec_test_1234567890abcdef1234567890abcdef";

    // Przykładowy payload checkout.session.completed (uproszczony)
    private const string SamplePayload = """
        {
          "id": "evt_test_001",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_abc123",
              "payment_status": "paid",
              "status": "complete"
            }
          }
        }
        """;

    /// <summary>
    /// Oblicza poprawny nagłówek Stripe-Signature dla podanych danych.
    /// Użyj tej metody w testach integracyjnych do generowania fixture'ów.
    /// </summary>
    public static string ComputeStripeSignature(string payload, string secret, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";
        var keyBytes   = Encoding.UTF8.GetBytes(secret.Replace("whsec_", string.Empty));
        var dataBytes  = Encoding.UTF8.GetBytes(signedPayload);
        var hmac       = HMACSHA256.HashData(keyBytes, dataBytes);
        var signature  = Convert.ToHexString(hmac).ToLowerInvariant();
        return $"t={ts},v1={signature}";
    }

    /// <summary>
    /// ConstructWebhookEvent akceptuje poprawny podpis HMAC-SHA256.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_ValidSignature_ReturnsEvent()
    {
        // Arrange
        var signature = ComputeStripeSignature(SamplePayload, TestWebhookSecret);

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        // Act
        var stripeEvent = caller.ConstructWebhookEvent(SamplePayload, signature);

        // Assert
        Assert.NotNull(stripeEvent);
        Assert.Equal("checkout.session.completed", stripeEvent.Type);
        Assert.Equal("evt_test_001", stripeEvent.Id);
    }

    /// <summary>
    /// ConstructWebhookEvent rzuca StripeException gdy podpis jest nieprawidłowy.
    /// </summary>
    [Fact]
    public void ConstructWebhookEvent_InvalidSignature_ThrowsStripeException()
    {
        // Arrange — poprawna forma nagłówka, ale zły klucz
        var wrongSecret   = "whsec_wrong_secret_0000000000000000";
        var badSignature  = ComputeStripeSignature(SamplePayload, wrongSecret);

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        // Act & Assert
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
        // Arrange — timestamp sprzed 10 minut
        var staleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var signature      = ComputeStripeSignature(SamplePayload, TestWebhookSecret, staleTimestamp);

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        // Act & Assert
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
        // Arrange — podpis wyliczony dla oryginalnego payloadu
        var signature      = ComputeStripeSignature(SamplePayload, TestWebhookSecret);
        var tamperedPayload = SamplePayload.Replace("paid", "unpaid");

        var caller = BuildCallerWithSecret(TestWebhookSecret);

        // Act & Assert
        Assert.Throws<StripeException>(() =>
            caller.ConstructWebhookEvent(tamperedPayload, signature));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IStripeServiceCaller BuildCallerWithSecret(string webhookSecret)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(
            [
                new($"Payments:Providers:Stripe:SecretKey",     "sk_test_unused"),
                new($"Payments:Providers:Stripe:WebhookSecret", webhookSecret),
                new($"Payments:Providers:Stripe:SuccessUrl",    "https://example.com/ok"),
                new($"Payments:Providers:Stripe:CancelUrl",     "https://example.com/cancel"),
            ]))
            .ConfigureServices((_, services) =>
            {
                services.RegisterStripeProvider();
                services.AddPayments().RegisterPaymentProvider<StripeProvider>();
            })
            .Build();

        return host.Services.GetRequiredService<IStripeServiceCaller>();
    }
}
