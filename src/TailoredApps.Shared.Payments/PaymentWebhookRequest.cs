#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

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

        private Dictionary<string, StringValues> headers = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, StringValues> query = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Parsed HTTP headers (e.g. Stripe-Signature, X-CashBill-Hmac).
        /// Keys are always compared case-insensitively (HTTP/2 lower-cases header names), even when
        /// a case-sensitive dictionary is assigned.
        /// </summary>
        public Dictionary<string, StringValues> Headers
        {
            get => headers;
            init => headers = CopyIgnoreCase(value);
        }

        /// <summary>
        /// Parsed query-string parameters (e.g. cmd, args, sign for CashBill).
        /// Keys are compared case-insensitively.
        /// </summary>
        public Dictionary<string, StringValues> Query
        {
            get => query;
            init => query = CopyIgnoreCase(value);
        }

        private static Dictionary<string, StringValues> CopyIgnoreCase(Dictionary<string, StringValues>? source)
        {
            var copy = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            if (source is null) return copy;
            foreach (var pair in source)
                copy[pair.Key] = pair.Value;
            return copy;
        }
    }
}
