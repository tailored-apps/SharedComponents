using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Caching.Hybrid;

namespace TailoredApps.Shared.MediatR.Caching;

/// <summary>
/// Gives <see cref="HybridCache"/> serializers built from the JSON metadata of a <see cref="JsonSerializerContext"/>,
/// which is generated at compile time. HybridCache serializes a cached response to keep a copy of it (and to
/// store it in a distributed cache); its built-in JSON fallback does that by reflection, which is not available
/// in an application that is trimmed or published with NativeAOT.
/// </summary>
/// <remarks>
/// The factory answers for the types the context has metadata for - the responses listed in its
/// <c>[JsonSerializable]</c> attributes and built-in types - and leaves every other type to the factories
/// registered before it. Register it with <c>PipelineRegistration.AddCacheSerialization(context)</c>.
/// </remarks>
public sealed class JsonContextCacheSerializerFactory : IHybridCacheSerializerFactory
{
    private readonly JsonSerializerContext context;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonContextCacheSerializerFactory"/>.
    /// </summary>
    /// <param name="context">The context whose metadata describes the cached response types.</param>
    public JsonContextCacheSerializerFactory(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
    }

    /// <inheritdoc/>
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = context.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo ? new Serializer<T>(typeInfo) : null;
        return serializer is not null;
    }

    private sealed class Serializer<T> : IHybridCacheSerializer<T>
    {
        private readonly JsonTypeInfo<T> typeInfo;

        public Serializer(JsonTypeInfo<T> typeInfo) => this.typeInfo = typeInfo;

        public T Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return JsonSerializer.Deserialize(ref reader, typeInfo)!;
        }

        public void Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, typeInfo);
        }
    }
}
