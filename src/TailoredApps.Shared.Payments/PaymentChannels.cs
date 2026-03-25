using System.Collections.Generic;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Represents a single payment channel offered by a payment provider
    /// (e.g. a specific bank transfer option, BLIK, or credit card).
    /// </summary>
    public class PaymentChannel
    {
        /// <summary>Gets or sets the unique identifier of the payment channel.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the payment model supported by this channel (one-time or subscription).</summary>
        public PaymentModel PaymentModel { get; set; }

        /// <summary>Gets or sets the list of ISO 4217 currency codes accepted by this channel.</summary>
        public List<string> AvailableCurrencies { get; set; }

        /// <summary>Gets or sets the display name of the payment channel.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets a human-readable description of the payment channel.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the URL of the channel's logo image.</summary>
        public string LogoUrl { get; set; }
    }
}
