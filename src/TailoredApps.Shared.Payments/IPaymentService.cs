using System.Collections.Generic;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    public interface IPaymentService
    {
        Task<ICollection<PaymentProvider>> GetProviders();
        Task<ICollection<PaymentChannel>> GetChannels(string providerId, string currency);
        Task<PaymentResponse> RegisterPayment(PaymentRequest request);
        Task<PaymentResponse> GetStatus(string providerId, string paymentId);
        Task<PaymentResponse> TransactionStatusChange(string providerId, TransactionStatusChangePayload payload);
    }
}
