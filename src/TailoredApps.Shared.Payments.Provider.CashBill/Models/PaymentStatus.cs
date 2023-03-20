using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    public class PaymentStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("paymentChannel")]
        public string PaymentChannel { get; set; }

        [JsonPropertyName("amount")]
        public Amount Amount { get; set; }

        [JsonPropertyName("requestedAmount")]
        public RequestedAmount RequestedAmount { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("personalData")]
        public PersonalData PersonalData { get; set; }

        [JsonPropertyName("additionalData")]
        public string AdditionalData { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonIgnore]
        public string PaymentProviderRedirectUrl { get; set; }
    }
}
