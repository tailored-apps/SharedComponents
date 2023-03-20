using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

namespace TailoredApps.Shared.MediatR.Caching
{
    public class Cache : ICache
    {
        private readonly IDistributedCache distributedCache;
        public Cache(IDistributedCache distributedCache)
        {
            this.distributedCache = distributedCache;
        }
        public async Task<T> GetAsync<T>(string cacheKey, CancellationToken cancellationToken)
        {
            var response = await distributedCache.GetAsync(cacheKey, cancellationToken);
            if (response == null)
            {
                return default(T);
            }
            var stringData = Encoding.UTF8.GetString(response);
            var serialized = (T)JsonConvert.DeserializeObject(stringData, typeof(T), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return serialized;
        }

        public async Task SetAsync<TResponse>(string cacheKey, TResponse response, TimeSpan? slidingExpiration, DateTime? absoluteExpiration, TimeSpan? absoluteExpirationRelativeToNow, CancellationToken cancellationToken)
        {
            var serializedObject = JsonConvert.SerializeObject(response);
            var bytes = Encoding.UTF8.GetBytes(serializedObject);

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration, AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow, SlidingExpiration = slidingExpiration };
            await distributedCache.SetAsync(cacheKey, bytes, cacheOptions, cancellationToken);
        }
    }
}
