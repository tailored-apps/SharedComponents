using System.Diagnostics.CodeAnalysis;

namespace TailoredApps.Shared.Payments;

/// <summary>
/// Encapsulates all information required to initiate a payment transaction
/// through a specific provider and channel.
/// </summary>
/// <remarks>
/// <see cref="PaymentProvider"/>, <see cref="Amount"/> and <see cref="Currency"/> are <c>required</c>: an
/// object initializer that leaves one out does not compile, and System.Text.Json refuses a document
/// without them.
/// </remarks>
public class PaymentRequest
{
    /// <summary>
    /// Initializes a new instance of <see cref="PaymentRequest"/>. The required members are set with an
    /// object initializer.
    /// </summary>
    public PaymentRequest()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentRequest"/> with its required members.
    /// </summary>
    /// <param name="paymentProvider">The key of the payment provider to use (e.g. "Stripe", "PayU").</param>
    /// <param name="amount">The payment amount in <paramref name="currency"/>.</param>
    /// <param name="currency">The ISO 4217 currency code (e.g. "PLN", "EUR").</param>
    [SetsRequiredMembers]
    public PaymentRequest(string paymentProvider, decimal amount, string currency)
    {
        PaymentProvider = paymentProvider;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets or sets the key of the payment provider to use (e.g. "Stripe", "PayU").</summary>
    public required string PaymentProvider { get; set; }

    /// <summary>Gets or sets the identifier of the specific payment channel within the provider.</summary>
    public string? PaymentChannel { get; set; }

    /// <summary>Gets or sets the billing model (one-time or subscription).</summary>
    public PaymentModel PaymentModel { get; set; }

    /// <summary>Gets or sets the short title or subject of the payment.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the detailed description displayed to the payer.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code (e.g. "PLN", "EUR").</summary>
    public required string Currency { get; set; }

    /// <summary>Gets or sets the payment amount in the specified currency.</summary>
    public required decimal Amount { get; set; }

    /// <summary>Gets or sets the payer's e-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the payer's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the payer's surname.</summary>
    public string? Surname { get; set; }

    /// <summary>Gets or sets the payer's street address.</summary>
    public string? Street { get; set; }

    /// <summary>Gets or sets the house/building number of the payer's address.</summary>
    public string? House { get; set; }

    /// <summary>Gets or sets the flat/apartment number of the payer's address.</summary>
    public string? Flat { get; set; }

    /// <summary>Gets or sets the postal code of the payer's address.</summary>
    public string? PostCode { get; set; }

    /// <summary>Gets or sets the city of the payer's address.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code of the payer's address.</summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets your own reference for this payment, for example an order number.
    /// </summary>
    /// <remarks>
    /// Providers pass it to the gateway as the merchant reference. Tpay and Adyen do not repeat the identifier
    /// returned by <see cref="IPaymentProvider.RequestPayment"/> in their notifications: for them this value is
    /// what comes back as <see cref="PaymentResponse.PaymentUniqueId"/> of a webhook result, so set it whenever
    /// a webhook has to be matched with an order.
    /// </remarks>
    public string? AdditionalData { get; set; }

    /// <summary>Gets or sets the referrer URL or identifier associated with this payment request.</summary>
    public string? Referer { get; set; }
}
