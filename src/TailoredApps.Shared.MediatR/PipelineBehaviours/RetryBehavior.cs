﻿using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.Interfaces.Messages;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IRetryableRequest<TRequest, TResponse>> _retryHandlers;
        private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;

        public RetryBehavior(IEnumerable<IRetryableRequest<TRequest, TResponse>> retryHandlers, ILogger<RetryBehavior<TRequest, TResponse>> logger)
        {
            _retryHandlers = retryHandlers;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var retryHandler = _retryHandlers.FirstOrDefault();
            if (retryHandler == null)
            {
                // No retry handler found, continue through pipeline
                return await next();
            }

            var circuitBreaker = Policy<TResponse>
                .Handle<Exception>()
                .CircuitBreakerAsync(retryHandler.ExceptionsAllowedBeforeCircuitTrip, TimeSpan.FromMilliseconds(5000),
                    (exception, things) =>
                    {
                        _logger.LogDebug("Circuit Tripped!");
                    },
                    () =>
                    {
                    });

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

            var response = await retryPolicy.ExecuteAsync(async () => await next());

            return response;
        }
    }
}
