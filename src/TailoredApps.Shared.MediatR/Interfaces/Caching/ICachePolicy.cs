using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching;

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
    /// Gets the moment at which the cache entry expires. Used only when
    /// <see cref="AbsoluteExpirationRelativeToNow"/> is <c>null</c>; a moment in the past means the
    /// response is not cached at all. Defaults to <c>null</c>.
    /// </summary>
    DateTime? AbsoluteExpiration => null;

    /// <summary>
    /// Gets how long the cache entry lives, counted from the moment it is stored. Takes precedence over
    /// <see cref="AbsoluteExpiration"/>. Defaults to <c>5 minutes</c>; when both are <c>null</c> the
    /// default of the registered <c>HybridCache</c> applies.
    /// </summary>
    TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets how long the in-process copy of the entry may be served before the distributed cache is
    /// consulted again. Defaults to <c>null</c>: the same lifetime as the entry itself.
    /// </summary>
    TimeSpan? LocalCacheExpiration => null;

    /// <summary>
    /// Gets the tags attached to the cache entry, so that related entries can be invalidated together
    /// with <c>HybridCache.RemoveByTagAsync</c>. Defaults to <c>null</c> (no tags).
    /// </summary>
    IReadOnlyCollection<string>? Tags => null;

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
