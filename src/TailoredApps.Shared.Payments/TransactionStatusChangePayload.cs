using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Wraps the raw data received from a payment provider's back-channel
    /// (status-change notification or legacy webhook).
    /// </summary>
    public class TransactionStatusChangePayload
    {
        /// <summary>
        /// Gets or sets the key of the payment provider that sent this notification.
        /// </summary>
        public string ProviderId { get; set; }

        /// <summary>
        /// Gets or sets the raw notification body (typically the deserialized HTTP request body).
        /// </summary>
        public object Payload { get; set; }

        /// <summary>
        /// Gets or sets the query-string parameters received with the notification request.
        /// </summary>
        public Dictionary<string, StringValues> QueryParameters { get; set; }
    }
}
