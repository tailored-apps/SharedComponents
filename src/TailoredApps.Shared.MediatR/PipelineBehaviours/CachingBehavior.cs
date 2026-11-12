using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ForkForward.Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours;

/// <summary>
/// MediatR pipeline behavior that caches responses in <see cref="HybridCache"/>. A request with an
/// <see cref="ICachePolicy{TRequest,TResponse}"/> is answered from the cache when an entry exists;
/// otherwise the handler runs and its response is stored according to the policy.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HybridCache"/> keeps an in-process copy of every entry and, when an
/// <c>IDistributedCache</c> is registered, a shared copy as well. Concurrent requests for the same key
/// wait for a single handler execution instead of each running the handler.
/// </para>
/// <para>
/// Every response is cached as it is returned, including <c>null</c> and default values such as
/// <c>0</c> or <c>false</c>. A handler that throws caches nothing.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<ICachePolicy<TRequest, TResponse>> _cachePolicies;
    private readonly HybridCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="CachingBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="cache">The cache that stores and retrieves responses.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="cachePolicies">The collection of cache policies applicable to the request/response pair.</param>
    /// <param name="timeProvider">
    /// Clock used to turn <see cref="ICachePolicy{TRequest,TResponse}.AbsoluteExpiration"/> into a
    /// lifetime; the system clock when not registered.
    /// </param>
    public CachingBehavior(
        HybridCache cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger,
        IEnumerable<ICachePolicy<TRequest, TResponse>> cachePolicies,
        TimeProvider? timeProvider = null)
    {
        _cache = cache;
        _logger = logger;
        _cachePolicies = cachePolicies;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Handles the request by returning the cached response when one exists, or by executing the next
    /// pipeline step and caching its response. If no cache policy is registered for the request type,
    /// the pipeline continues without caching.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">The next handler delegate in the pipeline.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The response, either from the cache or from the handler.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var cachePolicy = _cachePolicies.FirstOrDefault();
        if (cachePolicy == null)
        {
            // No cache policy found, so just continue through the pipeline
            return await next();
        }

        if (!TryCreateEntryOptions(cachePolicy, out var entryOptions))
        {
            // The policy's absolute expiration has already passed: there is nothing worth storing.
            return await next();
        }

        var cacheKey = cachePolicy.GetCacheKey(request);
        var handlerInvoked = false;

        var response = await _cache.GetOrCreateAsync(
            cacheKey,
            async _ =>
            {
                handlerInvoked = true;
                return await next();
            },
            entryOptions,
            cachePolicy.Tags,
            cancellationToken);

        if (handlerInvoked)
        {
            _logger.LogDebug("Cached response for {RequestType} with cache key: {CacheKey}", typeof(TRequest).FullName, cacheKey);
        }
        else
        {
            _logger.LogDebug("Response for {RequestType} retrieved from cache. CacheKey: {CacheKey}", typeof(TRequest).FullName, cacheKey);
        }

        return response;
    }

    /// <summary>
    /// Translates the policy into cache entry options. Returns <see langword="false"/> when the policy
    /// asks for an entry that would already be expired.
    /// </summary>
    private bool TryCreateEntryOptions(ICachePolicy<TRequest, TResponse> cachePolicy, out HybridCacheEntryOptions? entryOptions)
    {
        var expiration = cachePolicy.AbsoluteExpirationRelativeToNow;
        if (expiration is null && cachePolicy.AbsoluteExpiration is { } absoluteExpiration)
        {
            expiration = absoluteExpiration.ToUniversalTime() - _timeProvider.GetUtcNow().UtcDateTime;
        }

        if (expiration is { } lifetime && lifetime <= TimeSpan.Zero)
        {
            entryOptions = null;
            return false;
        }

        var localCacheExpiration = cachePolicy.LocalCacheExpiration;
        entryOptions = expiration is null && localCacheExpiration is null
            ? null // nothing specified: the defaults of the registered HybridCache apply
            : new HybridCacheEntryOptions { Expiration = expiration, LocalCacheExpiration = localCacheExpiration };
        return true;
    }
}
