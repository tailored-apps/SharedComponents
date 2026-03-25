using System.Text.Json.Serialization;

namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    /// <summary>
    /// Represents the payer's personal and contact data as returned by the CashBill API.
    /// </summary>
    public class PersonalData
    {
        /// <summary>Gets or sets the payer's first name.</summary>
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        /// <summary>Gets or sets the payer's surname.</summary>
        [JsonPropertyName("surname")]
        public string Surname { get; set; }

        /// <summary>Gets or sets the payer's e-mail address.</summary>
        [JsonPropertyName("email")]
        public string Email { get; set; }

        /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code of the payer's address.</summary>
        [JsonPropertyName("country")]
        public string Country { get; set; }

        /// <summary>Gets or sets the city of the payer's address.</summary>
        [JsonPropertyName("city")]
        public string City { get; set; }

        /// <summary>Gets or sets the postal code of the payer's address.</summary>
        [JsonPropertyName("postcode")]
        public string Postcode { get; set; }

        /// <summary>Gets or sets the street of the payer's address.</summary>
        [JsonPropertyName("street")]
        public string Street { get; set; }

        /// <summary>Gets or sets the house/building number of the payer's address.</summary>
        [JsonPropertyName("house")]
        public string House { get; set; }

        /// <summary>Gets or sets the flat/apartment number of the payer's address.</summary>
        [JsonPropertyName("flat")]
        public string Flat { get; set; }

        /// <summary>Gets or sets the IP address of the payer at the time of the payment.</summary>
        [JsonPropertyName("ip")]
        public string Ip { get; set; }
    }
}
