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
    public class PipelineRegistration : IPipelineRegistration
    {
        private readonly IServiceCollection serviceCollection;
        public PipelineRegistration(IServiceCollection serviceCollection)
        {
            this.serviceCollection = serviceCollection;
        }
        public void RegisterPipelineBehaviors()
        {
            // Register MediatR Pipeline Behaviors
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(FallbackBehavior<,>));
            serviceCollection.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));
        }
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

            // IFallbackHandler discovery and registration
            serviceCollection.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IRetryableRequest<,>)))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }
    }
}
