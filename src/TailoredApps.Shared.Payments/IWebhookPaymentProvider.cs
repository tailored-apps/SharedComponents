using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Extension of <see cref="IPaymentProvider"/> for gateways that push
    /// payment-status notifications via HTTP webhooks or back-channel calls.
    /// </summary>
    /// <remarks>
    /// Providers that support webhooks implement this interface in addition to
    /// <see cref="IPaymentProvider"/>.  The <see cref="IPaymentService"/> dispatches
    /// incoming HTTP requests to the correct provider automatically based on
    /// <see cref="IPaymentProvider.Key"/>.
    /// </remarks>
    public interface IWebhookPaymentProvider : IPaymentProvider
    {
        /// <summary>
        /// Processes an incoming HTTP webhook/back-channel notification
        /// from the payment gateway.
        /// </summary>
        /// <param name="request">
        /// Unified representation of the raw HTTP request
        /// (body, headers, query parameters, etc.).
        /// </param>
        /// <returns>
        /// <see cref="PaymentWebhookResult.Ok"/> — notification was processed,
        ///   <see cref="PaymentWebhookResult.PaymentResponse"/> contains the resolved status.<br/>
        /// <see cref="PaymentWebhookResult.Ignore"/> — notification was valid but irrelevant.<br/>
        /// <see cref="PaymentWebhookResult.Fail"/> — signature invalid, parse error, etc.
        /// </returns>
        Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request);
    }
}
