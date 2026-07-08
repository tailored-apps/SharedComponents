using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents a single payment channel returned by the CashBill
    /// GET /paymentchannels/{shopId} endpoint.
    /// </summary>
    public class PaymentChannels
    {
        /// <summary>Gets or sets the unique identifier of the payment channel.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Gets or sets the list of ISO 4217 currency codes accepted by this channel.</summary>
        [JsonPropertyName("availableCurrencies")]
        public List<string> AvailableCurrencies { get; set; }

        /// <summary>Gets or sets the display name of the payment channel.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Gets or sets a human-readable description of the payment channel.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>Gets or sets the URL of the channel's logo image.</summary>
        [JsonPropertyName("logoUrl")]
        public string LogoUrl { get; set; }
    }
}
