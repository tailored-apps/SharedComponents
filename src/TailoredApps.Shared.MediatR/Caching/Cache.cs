using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

namespace TailoredApps.Shared.MediatR.Caching
{
    /// <summary>
    /// Implementation of <see cref="ICache"/> backed by <see cref="IDistributedCache"/>.
    /// Serializes and deserializes cached objects using Newtonsoft.Json.
    /// </summary>
    public class Cache : ICache
    {
        private readonly IDistributedCache distributedCache;

        /// <summary>
        /// Initializes a new instance of <see cref="Cache"/>.
        /// </summary>
        /// <param name="distributedCache">The underlying distributed cache implementation.</param>
        public Cache(IDistributedCache distributedCache)
        {
            this.distributedCache = distributedCache;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task SetAsync<TResponse>(string cacheKey, TResponse response, TimeSpan? slidingExpiration, DateTime? absoluteExpiration, TimeSpan? absoluteExpirationRelativeToNow, CancellationToken cancellationToken)
        {
            var serializedObject = JsonConvert.SerializeObject(response);
            var bytes = Encoding.UTF8.GetBytes(serializedObject);

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration, AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow, SlidingExpiration = slidingExpiration };
            await distributedCache.SetAsync(cacheKey, bytes, cacheOptions, cancellationToken);
        }
    }
}
