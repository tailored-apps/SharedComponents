using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    /// <summary>
    /// MediatR pipeline behavior that implements a fallback strategy. When the primary handler throws
    /// an exception, the registered <see cref="IFallbackHandler{TRequest, TResponse}"/> is invoked
    /// to provide an alternative response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class FallbackBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IFallbackHandler<TRequest, TResponse>> _fallbackHandlers;
        private readonly ILogger<FallbackBehavior<TRequest, TResponse>> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="FallbackBehavior{TRequest, TResponse}"/>.
        /// </summary>
        /// <param name="fallbackHandlers">The collection of fallback handlers registered for this request/response pair.</param>
        /// <param name="logger">The logger instance used for diagnostic output.</param>
        public FallbackBehavior(IEnumerable<IFallbackHandler<TRequest, TResponse>> fallbackHandlers, ILogger<FallbackBehavior<TRequest, TResponse>> logger)
        {
            _fallbackHandlers = fallbackHandlers;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var fallbackHandler = _fallbackHandlers.FirstOrDefault();
            if (fallbackHandler == null)
            {
                return await next();
            }

            var fallbackPolicy = Policy<TResponse>
                .Handle<Exception>()
                .FallbackAsync(async (cancellationToken) =>
                {
                    _logger.LogDebug($"Initial handler failed. Falling back to `{fallbackHandler.GetType().FullName}@HandleFallback`");
                    return await fallbackHandler.HandleFallback(request, cancellationToken)
                        .ConfigureAwait(false);
                });

            var response = await fallbackPolicy.ExecuteAsync(async () => await next());

            return response;
        }
    }
}
