using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents the originally requested payment amount before any channel-level adjustments.
    /// Returned as part of the CashBill payment status response.
    /// </summary>
    public class RequestedAmount
    {
        /// <summary>Gets or sets the numeric amount value as originally requested.</summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>Gets or sets the ISO 4217 currency code for the requested amount.</summary>
        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; }
    }
}
