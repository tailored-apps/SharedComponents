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
    /// MediatR Caching Pipeline Behavior
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<ICachePolicy<TRequest, TResponse>> _cachePolicies;
        private readonly ICache _cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        public CachingBehavior(ICache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger, IEnumerable<ICachePolicy<TRequest, TResponse>> cachePolicies)
        {
            _cache = cache;
            _logger = logger;
            _cachePolicies = cachePolicies;
        }

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
