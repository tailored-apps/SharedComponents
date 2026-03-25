namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Defines the billing model of a payment channel.
    /// </summary>
    public enum PaymentModel
    {
        /// <summary>A single, non-recurring payment transaction.</summary>
        OneTime,

        /// <summary>A recurring subscription-based payment.</summary>
        Subscription
    }
}
