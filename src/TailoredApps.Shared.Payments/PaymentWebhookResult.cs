namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Result of processing an incoming payment gateway webhook.
    /// </summary>
    public class PaymentWebhookResult
    {
        /// <summary>True when the webhook was processed without an infrastructure error.</summary>
        public bool Success { get; private init; }

        /// <summary>
        /// True when the event was valid but irrelevant and should be silently discarded
        /// (e.g. Stripe <c>payment_method.attached</c> or future unknown events).
        /// </summary>
        public bool Ignored { get; private init; }

        /// <summary>
        /// Payment status resolved from the webhook.
        /// Null when <see cref="Ignored"/> is true or processing failed.
        /// </summary>
        public PaymentResponse? PaymentResponse { get; private init; }

        /// <summary>Human-readable error or ignore reason.</summary>
        public string? ErrorMessage { get; private init; }

        // ─── Factory methods ──────────────────────────────────────────────

        /// <summary>Creates a successful result carrying a resolved payment response.</summary>
        public static PaymentWebhookResult Ok(PaymentResponse response) =>
            new() { Success = true, PaymentResponse = response };

        /// <summary>
        /// Creates a result indicating the event was received but should be silently ignored.
        /// </summary>
        /// <param name="reason">Optional human-readable reason (for logging).</param>
        public static PaymentWebhookResult Ignore(string? reason = null) =>
            new() { Success = true, Ignored = true, ErrorMessage = reason };

        /// <summary>Creates a failed result (e.g. invalid signature, parse error).</summary>
        /// <param name="error">Error description.</param>
        public static PaymentWebhookResult Fail(string error) =>
            new() { Success = false, ErrorMessage = error };
    }
}
