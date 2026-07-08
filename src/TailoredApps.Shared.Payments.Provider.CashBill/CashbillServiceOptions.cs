namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// Configuration options for the CashBill payment provider.
    /// Bound from the <c>appsettings.json</c> section identified by <see cref="ConfigurationKey"/>.
    /// </summary>
    public class CashbillServiceOptions
    {
        /// <summary>Gets the configuration section key used to bind these options.</summary>
        public static string ConfigurationKey => "Payments:Providers:Cashbill";

        /// <summary>Gets or sets the URL to redirect the payer after a successful payment.</summary>
        public string ReturnUrl { get; set; }

        /// <summary>Gets or sets the URL to redirect the payer after a failed or cancelled payment.</summary>
        public string NegativeReturnUrl { get; set; }

        /// <summary>Gets or sets the base URL of the CashBill REST API.</summary>
        public string ServiceUrl { get; set; }

        /// <summary>Gets or sets the CashBill shop identifier.</summary>
        public string ShopId { get; set; }

        /// <summary>Gets or sets the secret phrase used to sign API requests and verify notifications.</summary>
        public string ShopSecretPhrase { get; set; }
    }
}
