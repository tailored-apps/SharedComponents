using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.MediatR.Interfaces.Messages;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours;

/// <summary>
/// MediatR pipeline behavior that retries a failing request and stops calling a handler that keeps
/// failing. It applies to requests with a registered <see cref="IRetryableRequest{TRequest,TResponse}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A failed request is retried <see cref="IRetryableRequest{TRequest,TResponse}.RetryAttempts"/> times.
/// When <see cref="IRetryableRequest{TRequest,TResponse}.ExceptionsAllowedBeforeCircuitTrip"/> requests
/// in a row have failed even after their retries, the circuit opens for five seconds: requests of that
/// type fail immediately with <see cref="CircuitBreakerOpenException"/> instead of reaching the handler.
/// After the break one request is let through as a trial; its outcome closes or re-opens the circuit.
/// </para>
/// <para>
/// A request cancelled by its caller is neither retried nor counted as a failure, so a disconnecting
/// client cannot open the circuit for everyone else.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(5);

    // The circuit breaker must outlive the (transient) behavior instance to ever trip,
    // so it is shared per closed request/response type and created from the first handler's configuration.
    private static readonly Lock CircuitBreakerLock = new();
    private static ConsecutiveFailureCircuitBreaker? _circuitBreaker;

    private readonly IEnumerable<IRetryableRequest<TRequest, TResponse>> _retryHandlers;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="retryHandlers">The retry configurations registered for the request/response pair.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="timeProvider">
    /// Clock used for the wait between retries and for the circuit break; the system clock when not registered.
    /// </param>
    public RetryBehavior(
        IEnumerable<IRetryableRequest<TRequest, TResponse>> retryHandlers,
        ILogger<RetryBehavior<TRequest, TResponse>> logger,
        TimeProvider? timeProvider = null)
    {
        _retryHandlers = retryHandlers;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Executes the next pipeline step with the retry and circuit-breaker rules of the request type.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">The next handler delegate in the pipeline.</param>
    /// <param name="cancellationToken">Token used to cancel the operation, including the wait between retries.</param>
    /// <returns>The response of the pipeline.</returns>
    /// <exception cref="CircuitBreakerOpenException">The circuit for this request type is open.</exception>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var retryHandler = _retryHandlers.FirstOrDefault();
        if (retryHandler == null)
        {
            // No retry handler found, continue through pipeline
            return await next();
        }

        // The breaker surrounds the retried operation, so a single request still exhausts its retries;
        // only requests that fail in a row open the circuit.
        var circuitBreaker = GetOrCreateCircuitBreaker(retryHandler);
        var isTrial = circuitBreaker.Enter(typeof(TRequest));

        try
        {
            var response = await ExecuteWithRetries(retryHandler, next, cancellationToken);
            circuitBreaker.OnSuccess();
            return response;
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            if (circuitBreaker.OnFailure(isTrial))
            {
                _logger.LogDebug(
                    "Circuit for {RequestType} opened for {BreakDuration}.",
                    typeof(TRequest).FullName,
                    BreakDuration);
            }

            throw;
        }
        catch (OperationCanceledException)
        {
            circuitBreaker.OnCancelled(isTrial);
            throw;
        }
    }

    private async Task<TResponse> ExecuteWithRetries(
        IRetryableRequest<TRequest, TResponse> retryHandler,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        for (var retryAttempt = 1; ; retryAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await next();
            }
            catch (Exception exception) when (IsFailure(exception) && retryAttempt <= retryHandler.RetryAttempts)
            {
                var retryDelay = retryHandler.RetryWithExponentialBackoff
                    ? TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * retryHandler.RetryDelay)
                    : TimeSpan.FromMilliseconds(retryHandler.RetryDelay);

                _logger.LogDebug(
                    exception,
                    "Retrying {RequestType}, attempt {RetryAttempt} after {RetryDelay}.",
                    typeof(TRequest).FullName,
                    retryAttempt,
                    retryDelay);

                await Task.Delay(retryDelay, _timeProvider, cancellationToken);
            }
        }
    }

    /// <summary>
    /// A cancellation is the caller giving up, not the handler failing.
    /// </summary>
    private static bool IsFailure(Exception exception) => exception is not OperationCanceledException;

    private ConsecutiveFailureCircuitBreaker GetOrCreateCircuitBreaker(IRetryableRequest<TRequest, TResponse> retryHandler)
    {
        if (_circuitBreaker == null)
        {
            lock (CircuitBreakerLock)
            {
                _circuitBreaker ??= new ConsecutiveFailureCircuitBreaker(
                    retryHandler.ExceptionsAllowedBeforeCircuitTrip,
                    BreakDuration,
                    _timeProvider);
            }
        }

        return _circuitBreaker;
    }
}

/// <summary>
/// Circuit breaker that opens after a number of failures in a row and, once the break has passed,
/// lets a single trial call decide whether to close again.
/// </summary>
internal sealed class ConsecutiveFailureCircuitBreaker
{
    private readonly Lock _gate = new();
    private readonly int _failuresBeforeOpening;
    private readonly TimeSpan _breakDuration;
    private readonly TimeProvider _timeProvider;

    private int _consecutiveFailures;
    private DateTimeOffset? _openUntil;
    private bool _trialInProgress;

    public ConsecutiveFailureCircuitBreaker(int failuresBeforeOpening, TimeSpan breakDuration, TimeProvider timeProvider)
    {
        _failuresBeforeOpening = Math.Max(1, failuresBeforeOpening);
        _breakDuration = breakDuration;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Admits a call or throws when the circuit is open.
    /// </summary>
    /// <returns><see langword="true"/> when the call is the single trial allowed after a break.</returns>
    public bool Enter(Type requestType)
    {
        lock (_gate)
        {
            if (_openUntil is not { } openUntil)
            {
                return false;
            }

            var remaining = openUntil - _timeProvider.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                throw new CircuitBreakerOpenException(requestType, remaining);
            }

            if (_trialInProgress)
            {
                // Someone is already probing the handler; do not pile further calls onto it.
                throw new CircuitBreakerOpenException(requestType, TimeSpan.Zero);
            }

            _trialInProgress = true;
            return true;
        }
    }

    public void OnSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openUntil = null;
            _trialInProgress = false;
        }
    }

    /// <returns><see langword="true"/> when this failure opened the circuit.</returns>
    public bool OnFailure(bool wasTrial)
    {
        lock (_gate)
        {
            if (wasTrial)
            {
                _trialInProgress = false;
                _openUntil = _timeProvider.GetUtcNow() + _breakDuration;
                return true;
            }

            if (_openUntil is not null)
            {
                // A call admitted before the circuit opened has now failed; the circuit is already open.
                return false;
            }

            _consecutiveFailures++;
            if (_consecutiveFailures < _failuresBeforeOpening)
            {
                return false;
            }

            _openUntil = _timeProvider.GetUtcNow() + _breakDuration;
            return true;
        }
    }

    /// <summary>A cancelled call says nothing about the handler: leave the state as it was.</summary>
    public void OnCancelled(bool wasTrial)
    {
        if (!wasTrial)
        {
            return;
        }

        lock (_gate)
        {
            _trialInProgress = false;
        }
    }
}
