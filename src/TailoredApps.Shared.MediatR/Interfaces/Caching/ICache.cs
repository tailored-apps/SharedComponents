using System;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    /// <summary>
    /// Abstraction over <c>IDistributedCache</c> that provides typed read and write operations
    /// with JSON serialization. Use this interface to interact with the underlying cache store
    /// without coupling to a specific serialization or caching technology.
    /// </summary>
    public interface ICache
    {
        /// <summary>
        /// Retrieves a value from the cache and deserializes it to the specified type.
        /// Returns the default value for <typeparamref name="T"/> if the key is not found.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the cached value into.</typeparam>
        /// <param name="cacheKey">The key used to look up the cached entry.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>The deserialized cached value, or <c>default</c> if no entry exists for the key.</returns>
        Task<T> GetAsync<T>(string cacheKey, CancellationToken cancellationToken);

        /// <summary>
        /// Serializes the given value and stores it in the cache under the specified key
        /// with the provided expiration options.
        /// </summary>
        /// <typeparam name="TResponse">The type of the object to serialize and cache.</typeparam>
        /// <param name="cacheKey">The key under which the value will be stored.</param>
        /// <param name="response">The object to serialize and cache.</param>
        /// <param name="slidingExpiration">Optional sliding expiration window.</param>
        /// <param name="absoluteExpiration">Optional absolute expiration date and time.</param>
        /// <param name="absoluteExpirationRelativeToNow">Optional absolute expiration relative to the current time.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        Task SetAsync<TResponse>(string cacheKey, TResponse response, TimeSpan? slidingExpiration, DateTime? absoluteExpiration, TimeSpan? absoluteExpirationRelativeToNow, CancellationToken cancellationToken);
    }
}
