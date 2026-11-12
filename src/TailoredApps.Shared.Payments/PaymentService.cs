using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Default implementation of <see cref="IPaymentService"/>.
    /// Resolves all registered <see cref="IPaymentProvider"/> instances at construction time
    /// and routes calls to the appropriate provider by key.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly ICollection<IPaymentProvider> paymentService;

        /// <summary>
        /// Initializes a new instance of <see cref="PaymentService"/> and resolves
        /// all registered <see cref="IPaymentProvider"/> instances from the service provider.
        /// </summary>
        /// <param name="serviceProvider">The application service provider.</param>
        public PaymentService(IServiceProvider serviceProvider)
        {
            this.paymentService = serviceProvider.GetServices<IPaymentProvider>().ToList();
        }

        /// <inheritdoc/>
        public async Task<ICollection<PaymentProvider>> GetProviders(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => paymentService.Select(x => new PaymentProvider { Id = x.Key, Name = x.Name }).ToList(), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ICollection<PaymentChannel>> GetChannels(string providerId, string currency, CancellationToken cancellationToken = default)
        {
            var channels = await paymentService.Single(x => x.Key == providerId).GetPaymentChannels(currency, cancellationToken);
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

        /// <inheritdoc/>
        public async Task<PaymentResponse> RegisterPayment(PaymentRequest request, CancellationToken cancellationToken = default)
        {
            var provider = paymentService.Single(x => x.Key == request.PaymentProvider);
            return await provider.RequestPayment(request, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<PaymentResponse> GetStatus(string providerId, string paymentId, CancellationToken cancellationToken = default)
        {
            var provider = paymentService.Single(x => x.Key == providerId);
            return await provider.GetStatus(paymentId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<PaymentResponse> TransactionStatusChange(string providerId, TransactionStatusChangePayload payload, CancellationToken cancellationToken = default)
        {
            payload.ProviderId = providerId;
            var provider = paymentService.Single(x => x.Key == providerId);
            return await provider.TransactionStatusChange(payload, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<PaymentWebhookResult> HandleWebhookAsync(string providerKey, PaymentWebhookRequest request, CancellationToken cancellationToken = default)
        {
            var provider = paymentService.FirstOrDefault(x => x.Key == providerKey);
            if (provider is null)
                return PaymentWebhookResult.Reject(PaymentWebhookRejection.UnknownProvider, $"Provider '{providerKey}' not found.");

            if (provider is not IWebhookPaymentProvider webhookProvider)
                return PaymentWebhookResult.Reject(PaymentWebhookRejection.NotSupported, $"Provider '{providerKey}' does not support webhook handling.");

            return await webhookProvider.HandleWebhookAsync(request, cancellationToken);
        }
    }
}
