using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    /// <summary>
    /// MediatR pipeline behavior that provides response caching. Before invoking the next handler,
    /// it checks whether a cached response already exists for the request. If found, the cached value
    /// is returned immediately; otherwise the handler is executed and the result is stored in the cache
    /// according to the active <see cref="ICachePolicy{TRequest,TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<ICachePolicy<TRequest, TResponse>> _cachePolicies;
        private readonly ICache _cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="CachingBehavior{TRequest, TResponse}"/>.
        /// </summary>
        /// <param name="cache">The cache service used to store and retrieve responses.</param>
        /// <param name="logger">The logger instance for diagnostic output.</param>
        /// <param name="cachePolicies">The collection of cache policies applicable to the request/response pair.</param>
        public CachingBehavior(ICache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger, IEnumerable<ICachePolicy<TRequest, TResponse>> cachePolicies)
        {
            _cache = cache;
            _logger = logger;
            _cachePolicies = cachePolicies;
        }

        /// <summary>
        /// Handles the pipeline request by checking the cache first. If a cached response exists it is returned
        /// immediately; otherwise the next delegate is invoked and the response is cached according to the active policy.
        /// </summary>
        /// <param name="request">The incoming MediatR request.</param>
        /// <param name="next">The delegate representing the next handler in the pipeline.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>The response, either retrieved from cache or produced by the next handler.</returns>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var cachePolicy = _cachePolicies.FirstOrDefault();
            if (cachePolicy == null)
            {
                // No cache policy found, so just continue through the pipeline
                return await next();
            }
            var cacheKey = cachePolicy.GetCacheKey(request);
            var cachedResponse = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);
            if (cachedResponse != null)
            {
                _logger.LogDebug($"Response retrieved {typeof(TRequest).FullName} from cache. CacheKey: {cacheKey}");
                return cachedResponse;
            }

            var response = await next();
            _logger.LogDebug($"Caching response for {typeof(TRequest).FullName} with cache key: {cacheKey}");

            await _cache.SetAsync(cacheKey, response, cachePolicy.SlidingExpiration, cachePolicy.AbsoluteExpiration, cachePolicy.AbsoluteExpirationRelativeToNow, cancellationToken);
            return response;
        }


    }
}
