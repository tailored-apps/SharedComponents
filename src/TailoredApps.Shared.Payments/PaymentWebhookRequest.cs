using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Raw HTTP data received from a payment gateway's webhook/backchannel call.
    /// Providers receive this unified structure and extract what they need.
    /// </summary>
    public class PaymentWebhookRequest
    {
        /// <summary>HTTP method (GET, POST, etc.).</summary>
        public string HttpMethod { get; init; } = "POST";

        /// <summary>Raw request body (JSON, form data, …). Null when the gateway sends no body.</summary>
        public string? Body { get; init; }

        /// <summary>Value of the Content-Type header.</summary>
        public string? ContentType { get; init; }

        /// <summary>Caller's remote IP address.</summary>
        public string? RemoteIp { get; init; }

        /// <summary>Full query string, e.g. "cmd=transStatusChanged&amp;args=TX123&amp;sign=abc".</summary>
        public string? QueryString { get; init; }

        /// <summary>
        /// Parsed HTTP headers (e.g. Stripe-Signature, X-CashBill-Hmac).
        /// Keys are treated case-insensitively.
        /// </summary>
        public Dictionary<string, StringValues> Headers { get; init; } = new();

        /// <summary>
        /// Parsed query-string parameters (e.g. cmd, args, sign for CashBill).
        /// </summary>
        public Dictionary<string, StringValues> Query { get; init; } = new();
    }
}
