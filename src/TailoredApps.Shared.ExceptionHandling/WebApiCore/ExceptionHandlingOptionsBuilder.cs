using Microsoft.Extensions.DependencyInjection;
using System;
using TailoredApps.Shared.ExceptionHandling.Interfaces;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    public class ExceptionHandlingOptionsBuilder : IExceptionHandlingOptionsBuilder
    {
        public IServiceCollection Services { get; private set; }
        public ExceptionHandlingOptionsBuilder(IServiceCollection services)
        {
            this.Services = services;
        }
        public IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>() where Provider : class, IExceptionHandlingProvider
        {
            Services.AddTransient<IExceptionHandlingProvider, Provider>();
            return this;
        }

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
