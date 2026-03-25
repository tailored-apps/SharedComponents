using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents the full payment status response returned by the CashBill API
    /// (GET /payment/{shopId}/{paymentId}).
    /// </summary>
    public class PaymentStatus
    {
        /// <summary>Gets or sets the CashBill-assigned unique identifier for this payment.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Gets or sets the identifier of the payment channel used.</summary>
        [JsonPropertyName("paymentChannel")]
        public string PaymentChannel { get; set; }

        /// <summary>Gets or sets the actual amount processed by the payment channel.</summary>
        [JsonPropertyName("amount")]
        public Amount Amount { get; set; }

        /// <summary>Gets or sets the originally requested amount before any channel adjustments.</summary>
        [JsonPropertyName("requestedAmount")]
        public RequestedAmount RequestedAmount { get; set; }

        /// <summary>Gets or sets the short title or subject of the payment.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>Gets or sets the detailed description of the payment.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>Gets or sets the personal data of the payer associated with this payment.</summary>
        [JsonPropertyName("personalData")]
        public PersonalData PersonalData { get; set; }

        /// <summary>Gets or sets any additional data attached when the payment was created.</summary>
        [JsonPropertyName("additionalData")]
        public string AdditionalData { get; set; }

        /// <summary>Gets or sets the current CashBill status string (e.g. "Start", "PositiveFinish").</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the redirect URL to the CashBill payment page.
        /// This field is populated locally after creation and is not part of the API response.
        /// </summary>
        [JsonIgnore]
        public string PaymentProviderRedirectUrl { get; set; }
    }
}
