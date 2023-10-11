using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly ICollection<IPaymentProvider> paymentService;
        public PaymentService(IServiceProvider serviceProvider)
        {
            this.paymentService = serviceProvider.GetServices<IPaymentProvider>().ToList();
        }

        public async Task<ICollection<PaymentProvider>> GetProviders()
        {
            return await Task.Run(() => paymentService.Select(x => new PaymentProvider { Id = x.Key, Name = x.Name }).ToList());
        }
        public async Task<ICollection<PaymentChannel>> GetChannels(string providerId, string currency)
        {
            var channels = await paymentService.Single(x => x.Key == providerId).GetPaymentChannels(currency);
            return channels.Select(x => new PaymentChannel
            {
                AvailableCurrencies = x.AvailableCurrencies,
                Id = x.Id,
                Description = x.Description,
                LogoUrl = x.LogoUrl,
                Name = x.Name,
                PaymentModel = x.PaymentModel

            }).ToList();
        }
        public async Task<PaymentResponse> RegisterPayment(PaymentRequest request)
        {
            var provider = paymentService.Single(x => x.Key == request.PaymentProvider);
            return await provider.RequestPayment(request);
        }

        public async Task<PaymentResponse> GetStatus(string providerId, string paymentId)
        {
            var provider = paymentService.Single(x => x.Key == providerId);
            return await provider.GetStatus(paymentId);
        }

        public async Task<PaymentResponse> TransactionStatusChange(string providerId, TransactionStatusChangePayload payload)
        {
            payload.ProviderId = providerId;
            var provider = paymentService.Single(x => x.Key == providerId);
            return await provider.TransactionStatusChange(payload);
        }
    }
}
