using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    /// <summary>Aggregates all registered payment providers and routes calls by provider key.</summary>
    public interface IPaymentService
    {
        /// <summary>Returns metadata for every registered provider.</summary>
        Task<ICollection<PaymentProvider>> GetProviders(CancellationToken cancellationToken = default);

        /// <summary>Returns available payment channels for the given provider and currency.</summary>
        /// <param name="providerId">Provider key (e.g. "Stripe", "CashBill").</param>
        /// <param name="currency">ISO 4217 currency code.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        Task<ICollection<PaymentChannel>> GetChannels(string providerId, string currency, CancellationToken cancellationToken = default);

        /// <summary>Initiates a new payment via the specified provider.</summary>
        /// <param name="request">Payment details including the target provider key.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        Task<PaymentResponse> RegisterPayment(PaymentRequest request, CancellationToken cancellationToken = default);

        /// <summary>Fetches the current payment status from the provider.</summary>
        /// <param name="providerId">Provider key.</param>
        /// <param name="paymentId">Provider-specific payment / transaction identifier.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        Task<PaymentResponse> GetStatus(string providerId, string paymentId, CancellationToken cancellationToken = default);

        /// <summary>Processes an incoming webhook/back-channel notification and returns the resolved status.</summary>
        /// <param name="providerId">Provider key.</param>
        /// <param name="payload">Legacy payload wrapper (body + query parameters).</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        Task<PaymentResponse> TransactionStatusChange(string providerId, TransactionStatusChangePayload payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches an incoming HTTP webhook request to the matching
        /// <see cref="IWebhookPaymentProvider"/> implementation.
        /// </summary>
        /// <param name="providerKey">Provider key that identifies which provider should handle the request.</param>
        /// <param name="request">Unified representation of the raw HTTP request.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>
        /// Processing result — <see cref="PaymentWebhookResult.Ignored"/> when the event is irrelevant,
        /// <see cref="PaymentWebhookResult.Fail"/> when the provider is not found or the signature is invalid.
        /// </returns>
        Task<PaymentWebhookResult> HandleWebhookAsync(string providerKey, PaymentWebhookRequest request, CancellationToken cancellationToken = default);
    }
}
