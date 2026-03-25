using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using System.Reflection;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.DI;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;

namespace TailoredApps.Shared.MediatR.DI
{
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
            // Register MediatR Pipeline Behaviors
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(FallbackBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));
        }

        /// <inheritdoc/>
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
    }
}
