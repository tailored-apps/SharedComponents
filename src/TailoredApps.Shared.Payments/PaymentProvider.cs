namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Lightweight DTO representing a registered payment provider.
    /// Returned by <see cref="IPaymentService.GetProviders"/> as a summary for UI listings.
    /// </summary>
    public class PaymentProvider
    {
        /// <summary>Gets or sets the unique key that identifies the provider (e.g. "Stripe", "PayU").</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the display name of the provider.</summary>
        public string Name { get; set; }
    }
}
