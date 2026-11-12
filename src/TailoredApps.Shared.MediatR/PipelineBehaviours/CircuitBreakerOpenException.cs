using System;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours;

/// <summary>
/// Thrown by <see cref="RetryBehavior{TRequest, TResponse}"/> when requests of a type keep failing and
/// the circuit for that type is open: the request was rejected without reaching its handler.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="CircuitBreakerOpenException"/>.
    /// </summary>
    /// <param name="requestType">The type of the rejected request.</param>
    /// <param name="retryAfter">How long the circuit stays open.</param>
    public CircuitBreakerOpenException(Type requestType, TimeSpan retryAfter)
        : base($"The circuit for '{requestType.FullName}' is open; the request was not sent to its handler.")
    {
        RequestType = requestType;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the type of the rejected request.</summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets how long the circuit stays open. Zero when the break has passed and another request is
    /// already probing the handler.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}
