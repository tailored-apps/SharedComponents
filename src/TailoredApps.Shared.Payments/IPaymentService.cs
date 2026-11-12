using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments;

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
    /// <remarks>
    /// A response with <see cref="PaymentStatusEnum.Rejected"/> means the gateway refused the request. A gateway
    /// that is failing or cannot be reached throws instead (see the exceptions): the payment may or may not
    /// have been created, so do not mark the order as rejected. CashBill and Stripe report such failures with
    /// <c>WebException</c> and <c>StripeException</c>.
    /// </remarks>
    /// <param name="request">Payment details including the target provider key.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="System.ArgumentException">The provider key or the currency of <paramref name="request"/> is blank.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount of <paramref name="request"/> is zero or negative.</exception>
    /// <exception cref="PaymentGatewayException">The gateway could not handle the call; the payment may or may not exist.</exception>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// The gateway could not be reached. <see cref="PaymentGatewayException"/> derives from this type, so catching it covers both.
    /// </exception>
    Task<PaymentResponse> RegisterPayment(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fetches the current payment status from the provider.</summary>
    /// <remarks>
    /// The returned status is always what the gateway reported. When the gateway is failing, refuses the query
    /// or cannot be reached, the call throws (see the exceptions): the state of the payment is then not known
    /// and an order must not be cancelled on that basis. CashBill and Stripe report such failures with
    /// <c>WebException</c> and <c>StripeException</c>.
    /// </remarks>
    /// <param name="providerId">Provider key.</param>
    /// <param name="paymentId">Provider-specific payment / transaction identifier.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="PaymentGatewayException">The gateway answered the query with a status outside 200-299.</exception>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// The gateway could not be reached. <see cref="PaymentGatewayException"/> derives from this type, so catching it covers both.
    /// </exception>
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
    /// <param name="request">
    /// Unified representation of the raw HTTP request. Pass the body exactly as it was received (not rebuilt from
    /// a bound model or form) and every header: gateways sign the raw body.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>
    /// Processing result — <see cref="PaymentWebhookResult.Ignored"/> when the event is irrelevant,
    /// <see cref="PaymentWebhookResult.Fail"/> when the provider is not found or the signature is invalid.
    /// </returns>
    Task<PaymentWebhookResult> HandleWebhookAsync(string providerKey, PaymentWebhookRequest request, CancellationToken cancellationToken = default);
}
