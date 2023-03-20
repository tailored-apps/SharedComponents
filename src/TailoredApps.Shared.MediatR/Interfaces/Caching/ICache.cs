using System;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    // ICache is a helper wrapper over IDistributedCache that adds some read-through cache methods, etc.
    public interface ICache
    {
        Task<T> GetAsync<T>(string cacheKey, CancellationToken cancellationToken);
        Task SetAsync<TResponse>(string cacheKey, TResponse response, TimeSpan? slidingExpiration, DateTime? absoluteExpiration, TimeSpan? absoluteExpirationRelativeToNow, CancellationToken cancellationToken);
    }
}