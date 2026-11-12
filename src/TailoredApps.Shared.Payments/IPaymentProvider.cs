using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments;

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
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Collection of available <see cref="PaymentChannel"/> objects.</returns>
    Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a new payment transaction via this provider.
    /// </summary>
    /// <param name="request">Payment details including amount, currency, and payer information.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>
    /// Response containing the redirect URL and payment unique identifier, or a response with
    /// <see cref="PaymentStatusEnum.Rejected"/> when the gateway refused the request.
    /// </returns>
    /// <exception cref="PaymentGatewayException">
    /// The gateway could not handle the call (5xx, 408, 429). The payment may or may not exist; it must not be
    /// reported as rejected.
    /// </exception>
    Task<PaymentResponse> RequestPayment(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status of an existing payment.
    /// </summary>
    /// <param name="paymentId">Provider-specific payment or transaction identifier.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Current <see cref="PaymentResponse"/> including the status the gateway reported.</returns>
    /// <exception cref="PaymentGatewayException">
    /// The gateway did not answer the query with a success status. The state of the payment is not known; it
    /// must not be reported as rejected.
    /// </exception>
    Task<PaymentResponse> GetStatus(string paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an incoming status-change notification (back-channel or legacy webhook).
    /// </summary>
    /// <param name="payload">Payload wrapper containing body, query parameters, and provider ID.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Resolved <see cref="PaymentResponse"/> with updated status.</returns>
    Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload, CancellationToken cancellationToken = default);
}
