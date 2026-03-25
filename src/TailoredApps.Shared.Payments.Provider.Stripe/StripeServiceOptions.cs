namespace TailoredApps.Shared.Payments.Provider.Stripe;

/// <summary>
/// Konfiguracja providera Stripe.
/// Sekcja w appsettings.json: <see cref="ConfigurationKey"/>.
/// </summary>
public class StripeServiceOptions
{
    /// <summary>Klucz sekcji konfiguracji.</summary>
    public static string ConfigurationKey => "Payments:Providers:Stripe";

    /// <summary>
    /// Klucz sekretny Stripe (sk_live_... lub sk_test_...).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Sekret webhooka (whsec_...) — do weryfikacji podpisu HMAC-SHA256.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// URL powrotu po udanej płatności.
    /// Stripe podmienia {CHECKOUT_SESSION_ID} na id sesji.
    /// Przykład: "https://example.com/payment/success?session={CHECKOUT_SESSION_ID}"
    /// </summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL powrotu po anulowaniu lub błędzie płatności.
    /// </summary>
    public string CancelUrl { get; set; } = string.Empty;
}
