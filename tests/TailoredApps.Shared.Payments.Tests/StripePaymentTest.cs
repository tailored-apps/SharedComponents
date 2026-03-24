using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Stripe;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Testy integracyjne providera Stripe.
///
/// Testy oznaczone <see cref="FactAttribute"/> bez [Trait] są uruchamiane zawsze.
/// Testy wymagające połączenia ze Stripe (sk_test_...) są pomijane gdy klucz nie jest ustawiony.
///
/// Aby uruchomić testy integracyjne, ustaw zmienne środowiskowe:
///   STRIPE_SECRET_KEY=sk_test_...
///   STRIPE_WEBHOOK_SECRET=whsec_...
/// lub wpisz wartości w appsettings.json (nie commituj kluczy produkcyjnych!).
/// </summary>
public class StripePaymentTest
{
    // ─── DI setup ────────────────────────────────────────────────────────────

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true);
                cfg.AddEnvironmentVariables("STRIPE_"); // STRIPE_SECRET_KEY, STRIPE_WEBHOOK_SECRET itp.
            })
            .ConfigureServices((_, services) =>
            {
                services.RegisterStripeProvider();
                services.AddPayments()
                    .RegisterPaymentProvider<StripeProvider>();
            })
            .Build();

    // ─── Unit / smoke tests (nie wymagają połączenia) ────────────────────────

    [Fact]
    public async Task CanRequestPaymentProviders_IncludesStripe()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var providers = await paymentService.GetProviders();

        Assert.Contains(providers, p => p.Id == "Stripe");
    }

    [Fact]
    public async Task GetChannels_Stripe_PLN_ReturnsBlikP24Card()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var channels = await paymentService.GetChannels("Stripe", "PLN");

        Assert.Contains(channels, c => c.Id == "card");
        Assert.Contains(channels, c => c.Id == "blik");
        Assert.Contains(channels, c => c.Id == "p24");
    }

    [Fact]
    public async Task GetChannels_Stripe_EUR_ReturnsCardAndSepa()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var channels = await paymentService.GetChannels("Stripe", "EUR");

        Assert.Contains(channels, c => c.Id == "card");
        Assert.Contains(channels, c => c.Id == "sepa_debit");
    }

    [Fact]
    public async Task GetChannels_Stripe_USD_ReturnsOnlyCard()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var channels = await paymentService.GetChannels("Stripe", "USD");

        Assert.Single(channels, c => c.Id == "card");
    }

    /// <summary>
    /// Webhook z nieprawidłowym podpisem zwraca Rejected (nie rzuca wyjątku na zewnątrz).
    /// </summary>
    [Fact]
    public async Task TransactionStatusChange_InvalidSignature_ReturnsRejected()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var result = await paymentService.TransactionStatusChange("Stripe", new TransactionStatusChangePayload
        {
            ProviderId = "Stripe",
            Payload    = """{"id":"evt_test","type":"checkout.session.completed"}""",
            QueryParameters = new Dictionary<string, StringValues>
            {
                { "Stripe-Signature", new StringValues("t=1,v1=invalidsignature") },
            },
        });

        Assert.Equal(PaymentStatusEnum.Rejected, result.PaymentStatus);
    }

    // ─── Testy integracyjne (wymagają sk_test_... z appsettings/env) ─────────

    /// <summary>
    /// Tworzy rzeczywistą Checkout Session w środowisku testowym Stripe.
    /// Wymaga STRIPE_SECRET_KEY=sk_test_...
    /// </summary>
    [Fact(Skip = "Integration test — requires STRIPE_SECRET_KEY=sk_test_... in env or appsettings.json")]
    public async Task RegisterPayment_Stripe_CreatesCheckoutSession()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        var providers = await paymentService.GetProviders();
        var channels  = await paymentService.GetChannels("Stripe", "PLN");

        var payment = await paymentService.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "Stripe",
            PaymentChannel  = "card",
            PaymentModel    = PaymentModel.OneTime,
            Title           = "Testowa płatność",
            Description     = "Integracyjny test Stripe Checkout",
            Currency        = "PLN",
            Amount          = 9.99m,
            Email           = "test@example.com",
            FirstName       = "Jan",
            Surname         = "Testowy",
            AdditionalData  = "test-integration",
            Referer         = "xunit",
        });

        Assert.NotNull(payment);
        Assert.Equal(PaymentStatusEnum.Created, payment.PaymentStatus);
        Assert.NotEmpty(payment.RedirectUrl!);
        Assert.StartsWith("cs_test_", payment.PaymentUniqueId);
        Assert.StartsWith("https://checkout.stripe.com/", payment.RedirectUrl);
    }

    /// <summary>
    /// Pobiera status Checkout Session po jej utworzeniu.
    /// Wymaga STRIPE_SECRET_KEY=sk_test_...
    /// </summary>
    [Fact(Skip = "Integration test — requires STRIPE_SECRET_KEY=sk_test_... in env or appsettings.json")]
    public async Task GetStatus_Stripe_CreatedSession_ReturnsCreated()
    {
        var host           = BuildHost();
        var paymentService = host.Services.GetRequiredService<IPaymentService>();

        // Utwórz sesję
        var payment = await paymentService.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "Stripe",
            PaymentChannel  = "card",
            PaymentModel    = PaymentModel.OneTime,
            Title           = "Status test",
            Currency        = "PLN",
            Amount          = 1.00m,
            Email           = "status@example.com",
        });

        // Sprawdź status
        var status = await paymentService.GetStatus("Stripe", payment.PaymentUniqueId!);

        Assert.NotNull(status);
        Assert.Equal(PaymentStatusEnum.Created, status.PaymentStatus);
        Assert.Equal(payment.PaymentUniqueId, status.PaymentUniqueId);
    }
}
