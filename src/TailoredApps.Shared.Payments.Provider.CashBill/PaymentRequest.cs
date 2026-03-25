namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// CashBill-specific payment request model passed to <see cref="ICashbillServiceCaller.GeneratePayment"/>.
    /// Contains all fields required by the CashBill API to create a new payment.
    /// </summary>
    public class PaymentRequest
    {
        /// <summary>Gets or sets the identifier of the CashBill payment channel.</summary>
        public string PaymentChannel { get; set; }

        /// <summary>Gets or sets the short title or subject of the payment.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the detailed description of the payment shown to the payer.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the ISO 4217 currency code (e.g. "PLN").</summary>
        public string Currency { get; set; }

        /// <summary>Gets or sets the payment amount in the specified currency.</summary>
        public decimal Amount { get; set; }

        /// <summary>Gets or sets the payer's e-mail address.</summary>
        public string Email { get; set; }

        /// <summary>Gets or sets the payer's first name.</summary>
        public string FirstName { get; set; }

        /// <summary>Gets or sets the payer's surname.</summary>
        public string Surname { get; set; }

        /// <summary>Gets or sets the payer's street address.</summary>
        public string Street { get; set; }

        /// <summary>Gets or sets the house/building number of the payer's address.</summary>
        public string House { get; set; }

        /// <summary>Gets or sets the flat/apartment number of the payer's address.</summary>
        public string Flat { get; set; }

        /// <summary>Gets or sets the postal code of the payer's address.</summary>
        public string PostCode { get; set; }

        /// <summary>Gets or sets the city of the payer's address.</summary>
        public string City { get; set; }

        /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code of the payer's address.</summary>
        public string Country { get; set; }

        /// <summary>Gets or sets any additional provider-specific data to attach to the payment.</summary>
        public string AdditionalData { get; set; }

        /// <summary>Gets or sets the referrer URL or identifier associated with this payment request.</summary>
        public string Referer { get; set; }
    }
}
