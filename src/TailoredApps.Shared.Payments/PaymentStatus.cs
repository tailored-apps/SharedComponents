namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Normalised payment status shared across all payment providers.
    /// Provider-specific status strings are mapped to these values.
    /// </summary>
    public enum PaymentStatusEnum
    {
        /// <summary>The payment has been created but the payer has not yet completed the transaction.</summary>
        Created,

        /// <summary>The payment is being processed by the provider and awaiting confirmation.</summary>
        Processing,

        /// <summary>The payment was completed successfully.</summary>
        Finished,

        /// <summary>The payment was rejected, cancelled, or failed.</summary>
        Rejected
    }
}
