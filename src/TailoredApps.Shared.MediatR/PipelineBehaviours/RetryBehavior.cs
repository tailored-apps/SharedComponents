using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using TailoredApps.Shared.MediatR.Interfaces.Messages;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    /// <summary>
    /// MediatR pipeline behavior that implements automatic retry with optional exponential backoff
    /// and a circuit breaker to prevent cascading failures.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        // The circuit breaker must outlive the (transient) behavior instance to ever trip,
        // so it is shared per closed request/response type and created from the first handler's configuration.
        private static readonly object _circuitBreakerLock = new object();
        private static AsyncCircuitBreakerPolicy<TResponse> _circuitBreaker;

        private readonly IEnumerable<IRetryableRequest<TRequest, TResponse>> _retryHandlers;
        private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="RetryBehavior{TRequest, TResponse}"/>.
        /// </summary>
        /// <param name="retryHandlers">The collection of retry configuration handlers registered for this request/response pair.</param>
        /// <param name="logger">The logger instance used for diagnostic output.</param>
        public RetryBehavior(IEnumerable<IRetryableRequest<TRequest, TResponse>> retryHandlers, ILogger<RetryBehavior<TRequest, TResponse>> logger)
        {
            _retryHandlers = retryHandlers;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var retryHandler = _retryHandlers.FirstOrDefault();
            if (retryHandler == null)
            {
                // No retry handler found, continue through pipeline
                return await next();
            }

            var circuitBreaker = GetOrCreateCircuitBreaker(retryHandler);

            var retryPolicy = Policy<TResponse>
                .Handle<Exception>()
                .WaitAndRetryAsync(retryHandler.RetryAttempts, retryAttempt =>
                {
                    var retryDelay = retryHandler.RetryWithExponentialBackoff
                        ? TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * retryHandler.RetryDelay)
                        : TimeSpan.FromMilliseconds(retryHandler.RetryDelay);

                    _logger.LogDebug($"Retrying, waiting {retryDelay}...");

                    return retryDelay;
                });

            // Breaker wraps the retried operation, so a single request still exhausts its retries;
            // only consecutively failing requests open the circuit and fail fast.
            var response = await circuitBreaker.WrapAsync(retryPolicy).ExecuteAsync(async () => await next());

            return response;
        }

        private AsyncCircuitBreakerPolicy<TResponse> GetOrCreateCircuitBreaker(IRetryableRequest<TRequest, TResponse> retryHandler)
        {
            if (_circuitBreaker == null)
            {
                lock (_circuitBreakerLock)
                {
                    if (_circuitBreaker == null)
                    {
                        _circuitBreaker = Policy<TResponse>
                            .Handle<Exception>()
                            .CircuitBreakerAsync(retryHandler.ExceptionsAllowedBeforeCircuitTrip, TimeSpan.FromMilliseconds(5000),
                                (exception, things) =>
                                {
                                    _logger.LogDebug("Circuit Tripped!");
                                },
                                () =>
                                {
                                });
                    }
                }
            }

            return _circuitBreaker;
        }
    }
}
