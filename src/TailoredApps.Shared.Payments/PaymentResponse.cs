namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Represents the result of a payment operation returned by a payment provider.
    /// </summary>
    public class PaymentResponse
    {
        /// <summary>
        /// Gets or sets the URL to which the payer should be redirected
        /// to complete the payment on the provider's hosted page.
        /// </summary>
        public string RedirectUrl { get; set; }

        /// <summary>
        /// Gets or sets the provider-assigned unique identifier for this payment or transaction.
        /// </summary>
        public string PaymentUniqueId { get; set; }

        /// <summary>
        /// Gets or sets the normalised payment status resolved from the provider's response.
        /// </summary>
        public PaymentStatusEnum PaymentStatus { get; set; }

        /// <summary>
        /// Gets or sets the raw or provider-specific response object for advanced scenarios.
        /// </summary>
        public object ResponseObject { get; set; }
    }
}
