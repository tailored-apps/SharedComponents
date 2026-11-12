namespace TailoredApps.Shared.Payments;

/// <summary>
/// Why an incoming payment webhook was rejected. Lets the host choose a response (for example 401 for a
/// bad signature, 400 for a malformed body) without inspecting message text.
/// </summary>
public enum PaymentWebhookRejection
{
    /// <summary>No specific reason was given (results created with <see cref="PaymentWebhookResult.Fail"/>).</summary>
    Unspecified = 0,

    /// <summary>The signature is missing or does not match. The request must be treated as forged.</summary>
    InvalidSignature,

    /// <summary>The provider's signing secret is not configured, so nothing can be verified.</summary>
    NotConfigured,

    /// <summary>The request cannot be read: required fields are missing or the body cannot be parsed.</summary>
    MalformedPayload,

    /// <summary>The notification was authentic, but confirming it with the provider failed.</summary>
    ProcessingFailed,

    /// <summary>No payment provider is registered under the requested key.</summary>
    UnknownProvider,

    /// <summary>The requested payment provider does not handle webhooks.</summary>
    NotSupported,
}
