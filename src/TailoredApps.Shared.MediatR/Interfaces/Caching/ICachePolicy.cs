using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    /// <summary>
    /// Defines the caching policy for a MediatR request/response pair.
    /// Provides default expiration values via C# 8.0 default interface implementations.
    /// Implement this interface to customise cache key generation or expiration strategy
    /// for a specific request type.
    /// </summary>
    /// <remarks>
    /// The default cache key is derived from the request type and a SHA-256 hash of the request's
    /// JSON representation, so requests that differ only in nested objects or collection contents get
    /// distinct keys and no property value ends up in plain text in the cache store. The key does
    /// <b>not</b> include the calling user or tenant: for per-user data, override
    /// <see cref="GetCacheKey"/> and prefix the key with the caller's identity, or responses will be
    /// shared between users.
    /// </remarks>
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
        /// Generates a cache key from the fully-qualified request type name and a SHA-256 hash of
        /// the request serialized as JSON (all public properties, including collections and nested objects).
        /// </summary>
        /// <param name="request">The MediatR request instance.</param>
        /// <returns>A string that uniquely identifies this request's content in the cache.</returns>
        string GetCacheKey(TRequest request)
        {
            return CacheKeyGenerator.Generate(typeof(TRequest), request);
        }
    }

    /// <summary>
    /// Builds content-based cache keys for MediatR requests.
    /// </summary>
    public static class CacheKeyGenerator
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        /// <summary>
        /// Returns <c>{RequestType.FullName}:{SHA-256 hex of the request JSON}</c>.
        /// </summary>
        /// <param name="requestType">The declared request type (used as the key namespace).</param>
        /// <param name="request">The request instance whose public state identifies the cache entry.</param>
        public static string Generate(Type requestType, object request)
        {
            if (requestType == null) throw new ArgumentNullException(nameof(requestType));

            var json = request is null
                ? "null"
                : JsonSerializer.Serialize(request, request.GetType(), SerializerOptions);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            return $"{requestType.FullName}:{hash}";
        }
    }
}
