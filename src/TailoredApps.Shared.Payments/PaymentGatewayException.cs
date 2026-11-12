using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments;

/// <summary>
/// Thrown when a payment gateway answers a call with an HTTP status that says nothing about the payment
/// itself: the gateway is failing or overloaded (5xx, 408, 429), or it refused a status query. The state
/// of the payment is then <b>not known</b> — it must not be treated as rejected.
/// </summary>
/// <remarks>
/// Derives from <see cref="HttpRequestException"/>, which is what a connection failure already throws, so
/// one <c>catch (HttpRequestException)</c> covers "the gateway could not be reached" and "the gateway
/// did not give a usable answer". A timeout still surfaces as <see cref="TaskCanceledException"/>.
/// </remarks>
public class PaymentGatewayException : HttpRequestException
{
    /// <summary>The longest part of the gateway's answer kept in <see cref="ResponseBody"/>.</summary>
    public const int MaxResponseBodyLength = 2048;

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentGatewayException"/>.
    /// </summary>
    /// <param name="provider">The key of the payment provider whose gateway answered.</param>
    /// <param name="statusCode">The HTTP status the gateway answered with.</param>
    /// <param name="responseBody">The body of the answer, for diagnostics; it is not part of the message.</param>
    public PaymentGatewayException(string provider, HttpStatusCode statusCode, string? responseBody = null)
        : this(provider, statusCode, responseBody, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentGatewayException"/> for an answer that the gateway's
    /// SDK reported through an exception of its own, which is kept as <see cref="Exception.InnerException"/>.
    /// </summary>
    /// <param name="provider">The key of the payment provider whose gateway answered.</param>
    /// <param name="statusCode">The HTTP status the gateway answered with.</param>
    /// <param name="responseBody">The body of the answer, for diagnostics; it is not part of the message.</param>
    /// <param name="innerException">The exception the gateway's SDK threw, with its own error details.</param>
    public PaymentGatewayException(string provider, HttpStatusCode statusCode, string? responseBody, Exception? innerException)
        : base($"Payment gateway '{provider}' answered {(int)statusCode} ({statusCode}); the state of the payment is not known.", innerException, statusCode)
    {
        Provider = provider;
        ResponseBody = responseBody is { Length: > MaxResponseBodyLength } ? responseBody[..MaxResponseBodyLength] : responseBody;
    }

    /// <summary>Gets the key of the payment provider whose gateway answered.</summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the beginning of the gateway's answer (at most <see cref="MaxResponseBodyLength"/> characters).
    /// It may hold details of the request; log it deliberately.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Gets a value indicating whether the same call may succeed later: the gateway was failing (5xx),
    /// timed out (408) or throttled the caller (429).
    /// </summary>
    public bool IsTransient => StatusCode is { } status && IsUnavailable(status);

    /// <summary>
    /// Tells whether a status means the gateway could not handle the call at all, as opposed to
    /// refusing it: any 5xx, 408 Request Timeout or 429 Too Many Requests.
    /// </summary>
    /// <param name="statusCode">The HTTP status the gateway answered with.</param>
    public static bool IsUnavailable(HttpStatusCode statusCode)
        => (int)statusCode >= 500 || statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    /// <summary>
    /// Throws when the gateway could not handle the call (see <see cref="IsUnavailable"/>). Use it for
    /// calls where a 4xx answer is a refusal the caller reports as such, for example creating a payment.
    /// </summary>
    /// <param name="provider">The key of the payment provider.</param>
    /// <param name="response">The gateway's answer.</param>
    /// <param name="cancellationToken">Cancels reading the answer.</param>
    /// <exception cref="PaymentGatewayException">The gateway answered with 5xx, 408 or 429.</exception>
    public static async Task ThrowIfUnavailableAsync(string provider, HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (IsUnavailable(response.StatusCode))
            throw await CreateAsync(provider, response, cancellationToken);
    }

    /// <summary>
    /// Throws when the gateway answered with anything but a success status. Use it for calls whose only
    /// useful answer is a success, for example reading the status of a payment: a 404 or a 401 there does
    /// not mean the payment was rejected.
    /// </summary>
    /// <param name="provider">The key of the payment provider.</param>
    /// <param name="response">The gateway's answer.</param>
    /// <param name="cancellationToken">Cancels reading the answer.</param>
    /// <exception cref="PaymentGatewayException">The gateway answered with a status outside 200-299.</exception>
    public static async Task ThrowIfNotSuccessAsync(string provider, HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.IsSuccessStatusCode)
            throw await CreateAsync(provider, response, cancellationToken);
    }

    private static async Task<PaymentGatewayException> CreateAsync(string provider, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
        return new PaymentGatewayException(provider, response.StatusCode, body);
    }
}
