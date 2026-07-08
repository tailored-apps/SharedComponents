using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    /// <summary>
    /// MediatR pipeline behavior that logs the execution time of every request and captures any
    /// exceptions that occur during handler processing.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of <see cref="LoggingBehavior{TRequest, TResponse}"/>.
        /// </summary>
        /// <param name="logger">The logger instance scoped to the request type.</param>
        public LoggingBehavior(ILogger<TRequest> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var correlationId = Guid.NewGuid();
            var timer = new System.Diagnostics.Stopwatch();
            using (var loggingScope = logger.BeginScope("{MeditatorRequestName} with {MeditatorRequestData}, correlation id {CorrelationId}", typeof(TRequest).Name, JsonSerializer.Serialize(request), correlationId))
            {
                try
                {
                    logger.LogDebug("Handler for {MeditatorRequestName} starting, correlation id {CorrelationId}", typeof(TRequest).Name, correlationId);
                    timer.Start();
                    var result = await next();
                    timer.Stop();
                    logger.LogDebug("Handler for {MeditatorRequestName} finished in {ElapsedMilliseconds}ms, correlation id {CorrelationId}", typeof(TRequest).Name, timer.Elapsed.TotalMilliseconds, correlationId);

                    return result;
                }
                catch (Exception e)
                {
                    timer.Stop();
                    logger.LogError(e, "Handler for {MeditatorRequestName} failed in {ElapsedMilliseconds}ms, correlation id {CorrelationId}\r\n" + e.StackTrace, typeof(TRequest).Name, timer.Elapsed.TotalMilliseconds, correlationId);
                    throw;
                }
            }
        }
    }
}
