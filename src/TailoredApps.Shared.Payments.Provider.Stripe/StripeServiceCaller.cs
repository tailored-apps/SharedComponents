using global::Stripe;
using global::Stripe.Checkout;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Payments.Provider.Stripe;

/// <summary>
/// Implementacja <see cref="IStripeServiceCaller"/> — wrapper nad oficjalnym Stripe.net SDK.
/// Używa per-request <see cref="RequestOptions"/> zamiast globalnego StripeConfiguration.ApiKey,
/// dzięki czemu obsługuje multi-tenant (różne klucze per żądanie).
/// </summary>
public class StripeServiceCaller : IStripeServiceCaller
{
    private readonly StripeServiceOptions options;
    private readonly IConfiguration? configuration;

    // Stripe.net services — wstrzykiwane przez DI (możliwe mockowanie w testach)
    private readonly SessionService sessionService;

    /// <summary>Inicjalizuje instancję callera.</summary>
    public StripeServiceCaller(IOptions<StripeServiceOptions> options, SessionService sessionService, IConfiguration? configuration = null)
    {
        this.options = options.Value;
        this.sessionService = sessionService;
        this.configuration = configuration;
    }

    private RequestOptions RequestOptions => new() { ApiKey = options.SecretKey };

    /// <inheritdoc/>
    public async Task<Session> CreateCheckoutSessionAsync(Payments.PaymentRequest request)
    {
        // Metody płatności — z konfiguracji (Stripe:AllowedPaymentMethods) lub domyślne per waluta
        var configuredMethods = configuration?
            .GetSection("Stripe:AllowedPaymentMethods")
            .Get<List<string>>();

        var paymentMethods = configuredMethods is { Count: > 0 }
            ? configuredMethods
            : GetPaymentMethodsForCurrency(request.Currency);

        var createOptions = new SessionCreateOptions
        {
            PaymentMethodTypes = paymentMethods,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency    = request.Currency.ToLowerInvariant(),
                        UnitAmount  = ToStripeAmount(request.Amount, request.Currency),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name        = request.Title,
                            Description = request.Description,
                        },
                    },
                    Quantity = 1,
                }
            ],
            Mode = "payment",
            SuccessUrl = options.SuccessUrl,
            CancelUrl = options.CancelUrl,
            CustomerEmail = request.Email,
            Metadata = new Dictionary<string, string>
            {
                { "additional_data", request.AdditionalData ?? string.Empty },
                { "referer",         request.Referer         ?? string.Empty },
                { "payment_channel", request.PaymentChannel  ?? string.Empty },
            },
        };

        return await sessionService.CreateAsync(createOptions, RequestOptions);
    }

    /// <inheritdoc/>
    public async Task<Session> GetCheckoutSessionAsync(string sessionId)
    {
        var getOptions = new SessionGetOptions
        {
            Expand = ["payment_intent"],
        };

        return await sessionService.GetAsync(sessionId, getOptions, RequestOptions);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// throwOnApiVersionMismatch=false — nie wymuszamy zgodności wersji API eventu z SDK.
    /// Stripe może wysyłać eventy ze starszą wersją API podczas przejść między wersjami.
    /// Weryfikacja podpisu HMAC-SHA256 jest zawsze wykonywana.
    /// </remarks>
    public Event ConstructWebhookEvent(string payload, string stripeSignature)
        => EventUtility.ConstructEvent(
            payload,
            stripeSignature,
            options.WebhookSecret,
            throwOnApiVersionMismatch: false);

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Konwertuje kwotę dziesiętną na najmniejszą jednostkę waluty (np. grosze dla PLN).
    /// Stripe wymaga kwot w najmniejszej jednostce (100 = 1,00 PLN).
    /// Waluty "zero-decimal" (np. JPY) podajemy bez mnożenia.
    /// </summary>
    private static long ToStripeAmount(decimal amount, string currency)
    {
        // Waluty bez podjednostek (zero-decimal currencies wg Stripe docs)
        HashSet<string> zeroDecimal =
        [
            "bif","clp","gnf","jpy","kmf","krw","mga","pyg","rwf","ugx","vnd","vuv","xaf","xof","xpf"
        ];

        return zeroDecimal.Contains(currency.ToLowerInvariant())
            ? (long)amount
            : (long)(amount * 100);
    }

    /// <summary>
    /// Zwraca domyślną listę metod płatności dla danej waluty.
    /// Można nadpisać przez konfigurację: <c>Stripe:AllowedPaymentMethods</c>.
    /// PLN: karta + BLIK (p24 wymaga aktywacji w Stripe Dashboard).
    /// EUR: karta + SEPA Direct Debit.
    /// Inne: tylko karta (najbezpieczniejszy fallback).
    /// </summary>
    private static List<string> GetPaymentMethodsForCurrency(string currency) =>
        currency.ToUpperInvariant() switch
        {
            "PLN" => ["card", "blik"],
            "EUR" => ["card", "sepa_debit"],
            _ => ["card"],
        };
}
