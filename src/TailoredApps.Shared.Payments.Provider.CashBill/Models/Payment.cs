using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents the minimal payment creation response returned by the CashBill API
    /// after a new payment is submitted (POST /payment/{shopId}).
    /// </summary>
    public class Payment
    {
        /// <summary>Gets or sets the CashBill-assigned unique identifier for this payment.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Gets or sets the URL to which the payer should be redirected to complete the payment.</summary>
        [JsonPropertyName("redirectUrl")]
        public string RedirectUrl { get; set; }
    }
}
