using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    public class RequestedAmount
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; }
    }
}
