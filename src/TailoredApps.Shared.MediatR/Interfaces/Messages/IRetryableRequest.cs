using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Messages
{
    /// <summary>
    /// Marker interface for MediatR requests that support retry and circuit-breaker behaviour.
    /// Implement this interface on a request class (or a dedicated policy class) to configure
    /// retry attempts, delay, exponential backoff, and circuit-breaker thresholds.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IRetryableRequest<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>Gets the number of retry attempts. Defaults to <c>1</c>.</summary>
        int RetryAttempts => 1;

        /// <summary>Gets the delay between retry attempts in milliseconds. Defaults to <c>250</c> ms.</summary>
        int RetryDelay => 250;

        /// <summary>
        /// Gets a value indicating whether exponential backoff should be applied between retries.
        /// Defaults to <c>false</c>.
        /// </summary>
        bool RetryWithExponentialBackoff => false;

        /// <summary>
        /// Gets the number of consecutive exceptions allowed before the circuit breaker trips.
        /// Defaults to <c>1</c>.
        /// </summary>
        int ExceptionsAllowedBeforeCircuitTrip => 1;
    }
}
