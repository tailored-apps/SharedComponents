using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;

namespace TailoredApps.Shared.ExceptionHandling.MinimalApi;

/// <summary>
/// Registration and endpoint conventions of ExceptionHandling for minimal APIs.
/// </summary>
public static class ExceptionHandlingMinimalApiExtensions
{
    /// <summary>
    /// Registers the exception handling services with the given provider. It registers exactly what
    /// <c>AddExceptionHandlingForWebApi</c> does - that call is not specific to controllers - under the name a
    /// minimal-API application looks for.
    /// </summary>
    /// <typeparam name="TExceptionHandlingProviderInterface">The provider interface to expose.</typeparam>
    /// <typeparam name="TExceptionHandlingProvider">The provider that maps exceptions to the error model.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>A builder for registering additional providers.</returns>
    public static IExceptionHandlingOptionsBuilder AddExceptionHandlingForMinimalApi<TExceptionHandlingProviderInterface, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TExceptionHandlingProvider>(this IServiceCollection services)
        where TExceptionHandlingProvider : class, TExceptionHandlingProviderInterface
        where TExceptionHandlingProviderInterface : class, IExceptionHandlingProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddExceptionHandlingForWebApi<TExceptionHandlingProviderInterface, TExceptionHandlingProvider>();
    }

    /// <summary>
    /// Answers exceptions thrown by the endpoint, or by every endpoint of the group, with the error model of
    /// the registered provider. See <see cref="ExceptionHandlingEndpointFilter"/>.
    /// </summary>
    /// <remarks>
    /// Filters run in the order they are added. Add this one first, so that it also sees the exceptions of
    /// the filters that follow it.
    /// </remarks>
    /// <typeparam name="TBuilder">A route handler builder or a route group builder.</typeparam>
    /// <param name="builder">The endpoint or the route group.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static TBuilder WithExceptionHandling<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new ExceptionHandlingEndpointFilter());
    }

    /// <summary>
    /// Validates the endpoint argument of type <typeparamref name="TRequest"/> with the registered
    /// FluentValidation validators before the endpoint runs. See <see cref="ValidationEndpointFilter{TRequest}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the endpoint argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new ValidationEndpointFilter<TRequest>());
    }

    /// <summary>
    /// Validates the argument of type <typeparamref name="TRequest"/> of every endpoint in the group that has
    /// one. See <see cref="ValidationEndpointFilter{TRequest}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the endpoint argument to validate.</typeparam>
    /// <param name="builder">The route group.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static RouteGroupBuilder WithValidation<TRequest>(this RouteGroupBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new ValidationEndpointFilter<TRequest>());
    }
}
