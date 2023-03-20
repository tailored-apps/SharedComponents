using System.Collections.Generic;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public interface ICashbillServiceCaller
    {
        Task<PaymentStatus> GeneratePayment(PaymentRequest request);
        Task<ICollection<PaymentChannels>> GetPaymentChannels(string currency);
        Task<PaymentStatus> GetPaymentStatus(string paymentId);
        Task<string> GetSignForNotificationService(TransactionStatusChanged transactionStatusChanged);
    }
}
