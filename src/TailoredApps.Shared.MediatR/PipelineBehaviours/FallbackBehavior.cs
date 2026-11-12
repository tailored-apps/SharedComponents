using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours;

/// <summary>
/// MediatR pipeline behavior that answers a request from an
/// <see cref="IFallbackHandler{TRequest,TResponse}"/> when the rest of the pipeline throws.
/// A request without a registered fallback handler passes through unchanged.
/// </summary>
/// <remarks>
/// A request cancelled by its caller is not sent to the fallback: the cancellation propagates.
/// </remarks>
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
    /// <param name="fallbackHandlers">The fallback handlers registered for the request/response pair.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public FallbackBehavior(IEnumerable<IFallbackHandler<TRequest, TResponse>> fallbackHandlers, ILogger<FallbackBehavior<TRequest, TResponse>> logger)
    {
        _fallbackHandlers = fallbackHandlers;
        _logger = logger;
    }

    /// <summary>
    /// Executes the next pipeline step and, if it throws, returns the fallback handler's response instead.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">The next handler delegate in the pipeline.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The response of the pipeline, or of the fallback handler when the pipeline failed.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var fallbackHandler = _fallbackHandlers.FirstOrDefault();
        if (fallbackHandler == null)
        {
            // No fallback handler found, continue through pipeline
            return await next();
        }

        try
        {
            return await next();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                exception,
                "Handler for {RequestType} failed. Falling back to {FallbackHandler}.",
                typeof(TRequest).FullName,
                fallbackHandler.GetType().FullName);

            return await fallbackHandler.HandleFallback(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
