using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{

    public class PaymentChannels
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("availableCurrencies")]
        public List<string> AvailableCurrencies { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("logoUrl")]
        public string LogoUrl { get; set; }
    }
}
