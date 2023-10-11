using System.Collections.Generic;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    public interface IPaymentProvider
    {

        string Key { get; }
        string Name { get; }
        string Description { get; }
        string Url { get; }

        Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency);
        Task<PaymentResponse> RequestPayment(PaymentRequest request);
        Task<PaymentResponse> GetStatus(string paymentId);
        Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload);
    }
}
