using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ForkForward.Mediator;

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
    /// <remarks>
    /// The request is serialized by reflection over its runtime type. In an application that is trimmed or
    /// published with NativeAOT reflection-based JSON is switched off, so this method throws there instead of
    /// producing a key from whatever members survived trimming. Such an application overrides
    /// <see cref="ICachePolicy{TRequest, TResponse}.GetCacheKey"/> in the cache policy, or builds the key with
    /// <see cref="Generate{TRequest}(TRequest, JsonTypeInfo{TRequest})"/>.
    /// </remarks>
    /// <param name="requestType">The declared request type (used as the key namespace).</param>
    /// <param name="request">The request instance whose public state identifies the cache entry.</param>
    /// <exception cref="InvalidOperationException">
    /// Reflection-based JSON serialization is not available in this application.
    /// </exception>
    public static string Generate(Type requestType, object request)
    {
        if (requestType == null) throw new ArgumentNullException(nameof(requestType));

        if (request is null)
            return Key(requestType, "null");

        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            throw new InvalidOperationException(
                $"A content-based cache key for '{requestType}' needs reflection-based JSON serialization, which is switched off in this " +
                "application (it is trimmed or published with NativeAOT). Override GetCacheKey in the cache policy of this request, or " +
                "build the key with CacheKeyGenerator.Generate(request, jsonTypeInfo).");
        }

        return Key(requestType, SerializeByReflection(request));
    }

    // Reached only when JsonSerializer.IsReflectionEnabledByDefault is true (checked by the caller). In a trimmed
    // or NativeAOT application that switch is off, Generate throws before this call, and nothing here runs. The
    // analyzers have no guard they recognise for reflection-based JSON, hence the explicit justification.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Called only when reflection-based JSON is enabled; see the comment above.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Called only when reflection-based JSON is enabled; see the comment above.")]
    private static string SerializeByReflection(object request)
        => JsonSerializer.Serialize(request, request.GetType(), SerializerOptions);

    /// <summary>
    /// Returns <c>{RequestType.FullName}:{SHA-256 hex of the request JSON}</c>, with the request serialized
    /// through <paramref name="jsonTypeInfo"/> - metadata generated at compile time by a
    /// <c>JsonSerializerContext</c>. It uses no reflection, so it works in a trimmed or NativeAOT application.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request instance whose state identifies the cache entry.</param>
    /// <param name="jsonTypeInfo">The JSON metadata of <typeparamref name="TRequest"/>.</param>
    public static string Generate<TRequest>(TRequest request, JsonTypeInfo<TRequest> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return Key(typeof(TRequest), request is null ? "null" : JsonSerializer.Serialize(request, jsonTypeInfo));
    }

    private static string Key(Type requestType, string json)
        => $"{requestType.FullName}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))}";
}
