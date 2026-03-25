using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents a monetary amount returned by the CashBill API,
    /// combining a numeric value and the corresponding currency code.
    /// </summary>
    public class Amount
    {
        /// <summary>Gets or sets the numeric amount value.</summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>Gets or sets the ISO 4217 currency code (e.g. "PLN").</summary>
        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; }
    }
}
