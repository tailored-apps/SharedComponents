using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{

    public class Payment
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("redirectUrl")]
        public string RedirectUrl { get; set; }
    }
}
