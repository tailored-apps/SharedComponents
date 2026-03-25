using System.Collections.Generic;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// Abstracts the CashBill API operations used by <see cref="CashBillProvider"/>.
    /// Handles payment creation, status polling, and back-channel signature verification.
    /// </summary>
    public interface ICashbillServiceCaller
    {
        /// <summary>
        /// Creates a new payment in the CashBill system and returns the initial payment status.
        /// </summary>
        /// <param name="request">Payment details including amount, currency, and payer data.</param>
        /// <returns>The initial <see cref="PaymentStatus"/> including the redirect URL.</returns>
        Task<PaymentStatus> GeneratePayment(PaymentRequest request);

        /// <summary>
        /// Retrieves the list of payment channels available for the given currency.
        /// </summary>
        /// <param name="currency">ISO 4217 currency code (e.g. "PLN").</param>
        /// <returns>Collection of available <see cref="PaymentChannels"/>.</returns>
        Task<ICollection<PaymentChannels>> GetPaymentChannels(string currency);

        /// <summary>
        /// Retrieves the current status of an existing payment from the CashBill API.
        /// </summary>
        /// <param name="paymentId">CashBill transaction identifier.</param>
        /// <returns>Current <see cref="PaymentStatus"/> for the given payment.</returns>
        Task<PaymentStatus> GetPaymentStatus(string paymentId);

        /// <summary>
        /// Computes the expected notification signature (MD5) for a back-channel status-change event.
        /// Used to verify the authenticity of incoming CashBill notifications.
        /// </summary>
        /// <param name="transactionStatusChanged">Notification data including command, transaction ID, and received sign.</param>
        /// <returns>Expected MD5 signature string.</returns>
        Task<string> GetSignForNotificationService(TransactionStatusChanged transactionStatusChanged);
    }
}
