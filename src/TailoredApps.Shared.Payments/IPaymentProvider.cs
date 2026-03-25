using System.Collections.Generic;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Core contract for a payment gateway integration.
    /// Each provider implementation represents a single payment gateway (e.g. Stripe, PayU).
    /// </summary>
    public interface IPaymentProvider
    {
        /// <summary>Gets the unique key used to identify this provider (e.g. "Stripe", "PayU").</summary>
        string Key { get; }

        /// <summary>Gets the display name of the provider.</summary>
        string Name { get; }

        /// <summary>Gets a human-readable description of the provider.</summary>
        string Description { get; }

        /// <summary>Gets the URL of the provider's website.</summary>
        string Url { get; }

        /// <summary>
        /// Returns the list of payment channels available for the given currency.
        /// </summary>
        /// <param name="currency">ISO 4217 currency code (e.g. "PLN", "EUR").</param>
        /// <returns>Collection of available <see cref="PaymentChannel"/> objects.</returns>
        Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency);

        /// <summary>
        /// Initiates a new payment transaction via this provider.
        /// </summary>
        /// <param name="request">Payment details including amount, currency, and payer information.</param>
        /// <returns>Response containing the redirect URL and payment unique identifier.</returns>
        Task<PaymentResponse> RequestPayment(PaymentRequest request);

        /// <summary>
        /// Retrieves the current status of an existing payment.
        /// </summary>
        /// <param name="paymentId">Provider-specific payment or transaction identifier.</param>
        /// <returns>Current <see cref="PaymentResponse"/> including status.</returns>
        Task<PaymentResponse> GetStatus(string paymentId);

        /// <summary>
        /// Processes an incoming status-change notification (back-channel or legacy webhook).
        /// </summary>
        /// <param name="payload">Payload wrapper containing body, query parameters, and provider ID.</param>
        /// <returns>Resolved <see cref="PaymentResponse"/> with updated status.</returns>
        Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload);
    }
}
