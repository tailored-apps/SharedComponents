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
    /// Pipeline behavior MediatR implementujący fallback — gdy główny handler rzuci wyjątkiem,
    /// wywołuje zarejestrowany <see cref="IFallbackHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">Typ żądania MediatR.</typeparam>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public class FallbackBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IFallbackHandler<TRequest, TResponse>> _fallbackHandlers;
        private readonly ILogger<FallbackBehavior<TRequest, TResponse>> _logger;

        /// <summary>Inicjalizuje instancję <see cref="FallbackBehavior{TRequest, TResponse}"/>.</summary>
        /// <param name="fallbackHandlers">Kolekcja fallback handlerów.</param>
        /// <param name="logger">Logger.</param>
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
