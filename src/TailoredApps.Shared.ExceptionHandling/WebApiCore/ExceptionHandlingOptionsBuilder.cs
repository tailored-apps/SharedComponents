using System;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    /// <summary>
    /// Fluent builder for configuring exception handling providers after the core services have been registered.
    /// </summary>
    public class ExceptionHandlingOptionsBuilder : IExceptionHandlingOptionsBuilder
    {
        /// <summary>
        /// Gets the underlying <see cref="IServiceCollection"/> used for DI registrations.
        /// </summary>
        public IServiceCollection Services { get; private set; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionHandlingOptionsBuilder"/>.
        /// </summary>
        /// <param name="services">The service collection to register providers into.</param>
        public ExceptionHandlingOptionsBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        /// <summary>
        /// Registers an additional <see cref="IExceptionHandlingProvider"/> implementation as a transient service.
        /// </summary>
        /// <typeparam name="Provider">The concrete provider type to register.</typeparam>
        /// <returns>The current builder instance for further chaining.</returns>
        public IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>() where Provider : class, IExceptionHandlingProvider
        {
            Services.AddTransient<IExceptionHandlingProvider, Provider>();
            return this;
        }

        /// <summary>
        /// Registers an additional <see cref="IExceptionHandlingProvider"/> implementation using a factory delegate.
        /// </summary>
        /// <typeparam name="Provider">The concrete provider type to register.</typeparam>
        /// <param name="implementationFactory">A factory delegate that creates the provider instance.</param>
        /// <returns>The current builder instance for further chaining.</returns>
        public IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>(Func<IServiceProvider, Provider> implementationFactory) where Provider : class, IExceptionHandlingProvider
        {
            Services.AddTransient<IExceptionHandlingProvider, Provider>(implementationFactory);
            return this;
        }
    }

    internal static class ExceptionHandlingOptionsBuilderExtensions
    {
        public static IExceptionHandlingOptionsBuilder AddExceptionHandling<TTargetExceptionHandlingProviderInterface, TTargetExceptionHandlingProvider>(this IServiceCollection services)
            where TTargetExceptionHandlingProvider : class, TTargetExceptionHandlingProviderInterface
            where TTargetExceptionHandlingProviderInterface : class, IExceptionHandlingProvider
        {
            services.AddTransient<TTargetExceptionHandlingProviderInterface, TTargetExceptionHandlingProvider>(container => container.GetRequiredService<TTargetExceptionHandlingProvider>());

            services.AddScoped<IExceptionHandlingService, ExceptionHandlingService<TTargetExceptionHandlingProvider>>();
            services.AddTransient<TTargetExceptionHandlingProviderInterface, TTargetExceptionHandlingProvider>();
            services.AddTransient<TTargetExceptionHandlingProvider, TTargetExceptionHandlingProvider>();

            return new ExceptionHandlingOptionsBuilder(services);
        }
    }
}
