namespace TailoredApps.Shared.Payments.Provider.CashBill.Models
{
    public class TransactionStatusChanged
    {
        public string Command { get; set; }
        public string TransactionId { get; set; }
        public string Sign { get; set; }
    }
}
