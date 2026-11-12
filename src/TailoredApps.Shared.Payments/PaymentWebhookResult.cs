namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Outcome of handling an incoming payment provider webhook. It is always exactly one of
    /// <see cref="PaymentWebhookProcessed"/>, <see cref="PaymentWebhookIgnored"/> or
    /// <see cref="PaymentWebhookRejected"/>.
    /// </summary>
    /// <remarks>
    /// The hierarchy is closed: no other case can exist, so a <c>switch</c> over the three cases is
    /// complete and the C# 15 compiler checks it. Code written against earlier versions can keep using
    /// <see cref="Success"/>, <see cref="Ignored"/>, <see cref="PaymentResponse"/> and <see cref="ErrorMessage"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// return result switch
    /// {
    ///     PaymentWebhookProcessed processed => Ok(processed.Payment.PaymentStatus),
    ///     PaymentWebhookIgnored => NoContent(),
    ///     PaymentWebhookRejected { Rejection: PaymentWebhookRejection.InvalidSignature } => Unauthorized(),
    ///     PaymentWebhookRejected => BadRequest(),
    /// };
    /// </code>
    /// </example>
    public closed record class PaymentWebhookResult
    {
        /// <summary>
        /// <see langword="true"/> when the webhook was accepted, that is processed or deliberately ignored;
        /// <see langword="false"/> when it was rejected.
        /// </summary>
        public bool Success => this is not PaymentWebhookRejected;

        /// <summary>
        /// <see langword="true"/> when the webhook was authentic but carried nothing to act on.
        /// </summary>
        public bool Ignored => this is PaymentWebhookIgnored;

        /// <summary>
        /// The payment state carried by a processed webhook; <see langword="null"/> for an ignored or rejected one.
        /// </summary>
        public PaymentResponse? PaymentResponse => (this as PaymentWebhookProcessed)?.Payment;

        /// <summary>
        /// The rejection message, or the reason a webhook was ignored; <see langword="null"/> when it was processed.
        /// </summary>
        public string? ErrorMessage => this switch
        {
            PaymentWebhookRejected rejected => rejected.Message,
            PaymentWebhookIgnored ignored => ignored.Reason,
            PaymentWebhookProcessed => null,
        };

        /// <summary>Creates the result of a webhook that carried a payment state to act on.</summary>
        /// <param name="response">The payment state reported by the provider.</param>
        public static PaymentWebhookResult Ok(PaymentResponse response) => new PaymentWebhookProcessed(response);

        /// <summary>Creates the result of an authentic webhook that requires no action.</summary>
        /// <param name="reason">Optional explanation, for logs.</param>
        public static PaymentWebhookResult Ignore(string? reason = null) => new PaymentWebhookIgnored(reason);

        /// <summary>Creates the result of an authentic webhook that requires no action, keeping what it reported.</summary>
        /// <param name="reason">Optional explanation, for logs.</param>
        /// <param name="payment">The non-actionable payment state the webhook reported, if any.</param>
        public static PaymentWebhookResult Ignore(string? reason, PaymentResponse? payment) => new PaymentWebhookIgnored(reason, payment);

        /// <summary>Creates a rejection without a specific reason. Prefer <see cref="Reject"/>.</summary>
        /// <param name="error">Message describing why the webhook was rejected.</param>
        public static PaymentWebhookResult Fail(string error) => new PaymentWebhookRejected(PaymentWebhookRejection.Unspecified, error);

        /// <summary>Creates a rejection with the reason the webhook was not accepted.</summary>
        /// <param name="rejection">Why the webhook was rejected.</param>
        /// <param name="message">Message for logs. Never put a secret or an expected signature in it.</param>
        public static PaymentWebhookResult Reject(PaymentWebhookRejection rejection, string message) => new PaymentWebhookRejected(rejection, message);

        /// <summary>
        /// Classifies the payment state parsed from an authentic notification: a payment that is still
        /// in progress requires no action and is ignored, every other state is processed.
        /// </summary>
        /// <param name="payment">The payment state parsed from the notification.</param>
        public static PaymentWebhookResult FromStatus(PaymentResponse payment)
            => payment.PaymentStatus == PaymentStatusEnum.Processing
                ? Ignore("Non-actionable event", payment)
                : Ok(payment);

        /// <summary>
        /// Converts the result into the <see cref="Payments.PaymentResponse"/> shape returned by
        /// <see cref="IPaymentProvider.TransactionStatusChange"/>: a rejection becomes a response with
        /// <see cref="PaymentStatusEnum.Rejected"/> and the message in <c>ResponseObject</c>.
        /// </summary>
        public PaymentResponse ToPaymentResponse() => this switch
        {
            PaymentWebhookProcessed processed => processed.Payment,
            PaymentWebhookIgnored ignored => ignored.Payment ?? new PaymentResponse { PaymentStatus = PaymentStatusEnum.Processing },
            PaymentWebhookRejected rejected => new PaymentResponse { PaymentStatus = PaymentStatusEnum.Rejected, ResponseObject = rejected.Message },
        };
    }

    /// <summary>The webhook was authentic and reported a payment state to act on.</summary>
    /// <param name="Payment">The payment state reported by the provider.</param>
    public sealed record class PaymentWebhookProcessed(PaymentResponse Payment) : PaymentWebhookResult;

    /// <summary>The webhook was authentic but requires no action, for example an event type the application does not track.</summary>
    /// <param name="Reason">Optional explanation, for logs.</param>
    /// <param name="Payment">The non-actionable payment state the webhook reported, if any.</param>
    public sealed record class PaymentWebhookIgnored(string? Reason, PaymentResponse? Payment = null) : PaymentWebhookResult;

    /// <summary>The webhook was not accepted. Nothing it claims may be acted on.</summary>
    /// <param name="Rejection">Why the webhook was rejected.</param>
    /// <param name="Message">Message for logs.</param>
    public sealed record class PaymentWebhookRejected(PaymentWebhookRejection Rejection, string Message) : PaymentWebhookResult;
}
