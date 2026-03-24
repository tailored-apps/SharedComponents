using global::Stripe;
using global::Stripe.Checkout;

namespace TailoredApps.Shared.Payments.Provider.Stripe;

/// <summary>
/// Abstrakcja nad Stripe SDK — ułatwia mockowanie w testach.
/// </summary>
public interface IStripeServiceCaller
{
    /// <summary>Tworzy Stripe Checkout Session i zwraca jej dane.</summary>
    Task<Session> CreateCheckoutSessionAsync(Payments.PaymentRequest request);

    /// <summary>Pobiera Checkout Session po ID (rozszerzony o PaymentIntent).</summary>
    Task<Session> GetCheckoutSessionAsync(string sessionId);

    /// <summary>
    /// Weryfikuje podpis webhooka i zwraca sparsowane zdarzenie Stripe.
    /// Rzuca <see cref="StripeException"/> gdy podpis jest nieprawidłowy.
    /// </summary>
    Event ConstructWebhookEvent(string payload, string stripeSignature);
}
