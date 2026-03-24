using global::Stripe;
using global::Stripe.Checkout;
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

    // Stripe.net services — wstrzykiwane przez DI (możliwe mockowanie w testach)
    private readonly SessionService sessionService;

    public StripeServiceCaller(IOptions<StripeServiceOptions> options, SessionService sessionService)
    {
        this.options = options.Value;
        this.sessionService = sessionService;
    }

    private RequestOptions RequestOptions => new() { ApiKey = options.SecretKey };

    /// <inheritdoc/>
    public async Task<Session> CreateCheckoutSessionAsync(Payments.PaymentRequest request)
    {
        // Metody płatności zależne od waluty
        var paymentMethods = GetPaymentMethodsForCurrency(request.Currency);

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
            Mode         = "payment",
            SuccessUrl   = options.SuccessUrl,
            CancelUrl    = options.CancelUrl,
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
    public Event ConstructWebhookEvent(string payload, string stripeSignature)
        => EventUtility.ConstructEvent(payload, stripeSignature, options.WebhookSecret);

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
    /// Zwraca listę metod płatności dostępnych dla danej waluty.
    /// PLN: karta + BLIK + Przelewy24 (p24).
    /// Inne: tylko karta (najbezpieczniejszy fallback).
    /// </summary>
    private static List<string> GetPaymentMethodsForCurrency(string currency) =>
        currency.ToUpperInvariant() switch
        {
            "PLN" => ["card", "blik", "p24"],
            "EUR" => ["card", "sepa_debit"],
            _     => ["card"],
        };
}
