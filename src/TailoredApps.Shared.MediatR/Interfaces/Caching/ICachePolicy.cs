using System;
using System.Linq;
using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    /// <summary>
    /// Defines the caching policy for a MediatR request/response pair.
    /// Provides default expiration values via C# 8.0 default interface implementations.
    /// Implement this interface to customise cache key generation or expiration strategy
    /// for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface ICachePolicy<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Gets the absolute expiration date and time for the cache entry.
        /// Defaults to <c>null</c> (no absolute expiration).
        /// </summary>
        DateTime? AbsoluteExpiration => null;

        /// <summary>
        /// Gets the absolute expiration time relative to now.
        /// Defaults to <c>5 minutes</c>.
        /// </summary>
        TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets the sliding expiration window. The cache entry expires if it has not been
        /// accessed within this time span. Defaults to <c>30 seconds</c>.
        /// </summary>
        TimeSpan? SlidingExpiration => TimeSpan.FromSeconds(30);

        /// <summary>
        /// Generates a unique cache key based on the fully-qualified request type name
        /// and the values of all its public properties.
        /// </summary>
        /// <param name="request">The MediatR request instance.</param>
        /// <returns>A string that uniquely identifies this request in the cache.</returns>
        string GetCacheKey(TRequest request)
        {
            var r = new { request };
            var props = r.request.GetType().GetProperties().Select(pi => $"{pi.Name}:{pi.GetValue(r.request, null)}");
            return $"{typeof(TRequest).FullName}{{{string.Join(",", props)}}}";
        }
    }
}
