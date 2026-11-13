using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;
using ForkForward.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrutor;
using TailoredApps.Shared.MediatR.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.DI;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;

namespace TailoredApps.Shared.MediatR.DI;

/// <summary>
/// Default implementation of <see cref="IPipelineRegistration"/> that registers all standard
/// MediatR pipeline behaviors (Logging, Validation, Caching, Fallback, Retry) into the
/// dependency injection container.
/// </summary>
public class PipelineRegistration : IPipelineRegistration
{
    private readonly IServiceCollection serviceCollection;

    /// <summary>
    /// Initializes a new instance of <see cref="PipelineRegistration"/>.
    /// </summary>
    /// <param name="serviceCollection">The DI service collection to register behaviors into.</param>
    public PipelineRegistration(IServiceCollection serviceCollection)
    {
        this.serviceCollection = serviceCollection;
    }

    /// <inheritdoc/>
    public void RegisterPipelineBehaviors()
    {
        // CachingBehavior stores responses in HybridCache: an in-process cache that also uses a
        // registered IDistributedCache. Nothing is replaced if the host has already added one.
        serviceCollection.AddHybridCache();

        // Register MediatR Pipeline Behaviors
        serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(FallbackBehavior<,>));
        serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Cache policies, fallback handlers and retry settings are found by scanning the assembly with reflection, which trimming breaks. Register them one by one with AddCachePolicy, AddFallbackHandler and AddRetryPolicy on PipelineRegistration.")]
    [RequiresDynamicCode("Cache policies, fallback handlers and retry settings are found by scanning the assembly with reflection, which trimming breaks. Register them one by one with AddCachePolicy, AddFallbackHandler and AddRetryPolicy on PipelineRegistration.")]
    public void RegisterPipelineBehaviors(Assembly assembly)
    {
        // ICachePolicy discovery and registration
        serviceCollection.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICachePolicy<,>)))
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        // IFallbackHandler discovery and registration
        serviceCollection.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IFallbackHandler<,>)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        // IRetryableRequest discovery and registration
        serviceCollection.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IRetryableRequest<,>)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsImplementedInterfaces()
            .WithTransientLifetime());
    }

    // ─── Registration without scanning: trimming and NativeAOT ──────────────────────────────────────────

    /// <summary>
    /// Registers the behaviors - logging, validation, caching, fallback, retry, in that order - for one
    /// request, as closed generic types. Use it instead of <see cref="RegisterPipelineBehaviors()"/> in an
    /// application published with NativeAOT for every request whose response is a value type: the container
    /// cannot close the open behaviors over a value type there. For requests with a reference-type response
    /// the open registration works natively too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Do not combine it with <see cref="RegisterPipelineBehaviors()"/> for the same request: the behaviors
    /// would then run twice.
    /// </para>
    /// <para>
    /// The caching behavior brings in <c>HybridCache</c>. Its package still contains a JSON fallback that works
    /// by reflection, so publishing with NativeAOT reports trim and AOT warnings from the
    /// <c>Microsoft.Extensions.Caching.Hybrid</c> assembly. The cache itself works natively when the cached
    /// responses are serialized with generated metadata, see <see cref="AddCacheSerialization"/>. For a request
    /// that is never cached use <see cref="AddPipelineBehaviorsWithoutCaching{TRequest, TResponse}"/>: an
    /// application that registers every request that way does not reference HybridCache at all.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type of the request.</typeparam>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddPipelineBehaviors<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        serviceCollection.AddHybridCache();

        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, LoggingBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, ValidationBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, CachingBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, FallbackBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, RetryBehavior<TRequest, TResponse>>();
        return this;
    }

    /// <summary>
    /// Registers the behaviors without the cache - logging, validation, fallback, retry, in that order - for one
    /// request, as closed generic types. It is a method of its own, not a switch on
    /// <see cref="AddPipelineBehaviors{TRequest, TResponse}"/>, so that the trimmer can drop HybridCache from an
    /// application that never caches: a branch on a run-time flag would keep it.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type of the request.</typeparam>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddPipelineBehaviorsWithoutCaching<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, LoggingBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, ValidationBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, FallbackBehavior<TRequest, TResponse>>();
        serviceCollection.AddTransient<IPipelineBehavior<TRequest, TResponse>, RetryBehavior<TRequest, TResponse>>();
        return this;
    }

    /// <summary>
    /// Registers the cache policy of a request by naming it - what <see cref="RegisterPipelineBehaviors(Assembly)"/>
    /// finds by scanning, without reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type of the request.</typeparam>
    /// <typeparam name="TPolicy">The cache policy.</typeparam>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddCachePolicy<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>()
        where TRequest : IRequest<TResponse>
        where TPolicy : class, ICachePolicy<TRequest, TResponse>
    {
        serviceCollection.AddTransient<ICachePolicy<TRequest, TResponse>, TPolicy>();
        return this;
    }

    /// <summary>
    /// Registers the fallback handler of a request by naming it. As with scanning, the first handler registered
    /// for a request is kept.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type of the request.</typeparam>
    /// <typeparam name="THandler">The fallback handler.</typeparam>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddFallbackHandler<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IFallbackHandler<TRequest, TResponse>
    {
        serviceCollection.TryAddTransient<IFallbackHandler<TRequest, TResponse>, THandler>();
        return this;
    }

    /// <summary>
    /// Registers the retry settings of a request by naming them. As with scanning, the first settings registered
    /// for a request are kept.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type of the request.</typeparam>
    /// <typeparam name="TRetry">The retry settings.</typeparam>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddRetryPolicy<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRetry>()
        where TRequest : IRequest<TResponse>
        where TRetry : class, IRetryableRequest<TRequest, TResponse>
    {
        serviceCollection.TryAddTransient<IRetryableRequest<TRequest, TResponse>, TRetry>();
        return this;
    }

    /// <summary>
    /// Makes the response cache serialize with the JSON metadata of <paramref name="context"/>, generated at
    /// compile time, instead of reflection. An application that is trimmed or published with NativeAOT needs
    /// it for every cached response that is not a built-in type: list those response types in the context's
    /// <c>[JsonSerializable]</c> attributes.
    /// </summary>
    /// <param name="context">The context whose metadata describes the cached response types.</param>
    /// <returns>This registration, for chaining.</returns>
    public PipelineRegistration AddCacheSerialization(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        serviceCollection.AddHybridCache().AddSerializerFactory(new JsonContextCacheSerializerFactory(context));
        return this;
    }
}
