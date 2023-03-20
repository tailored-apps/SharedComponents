namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public class CashbillServiceOptions
    {
        public static string ConfigurationKey => "Payments:Providers:Cashbill";

        public string ReturnUrl { get; set; }
        public string NegativeReturnUrl { get; set; }
        public string ServiceUrl { get; set; }
        public string ShopId { get; set; }
        public string ShopSecretPhrase { get; set; }
    }
}
