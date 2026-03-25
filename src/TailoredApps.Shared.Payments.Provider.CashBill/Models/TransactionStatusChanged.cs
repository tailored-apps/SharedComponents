namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents the back-channel notification payload sent by CashBill
    /// when a transaction status changes (e.g. payment completed or rejected).
    /// Parameters are delivered via HTTP query string.
    /// </summary>
    public class TransactionStatusChanged
    {
        /// <summary>
        /// Gets or sets the event command type sent by CashBill (e.g. "transactionStatusChanged").
        /// Corresponds to the <c>cmd</c> query parameter.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets the CashBill transaction identifier for the affected payment.
        /// Corresponds to the <c>args</c> query parameter.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the MD5 signature sent by CashBill for request authenticity verification.
        /// Corresponds to the <c>sign</c> query parameter.
        /// </summary>
        public string Sign { get; set; }
    }
}
