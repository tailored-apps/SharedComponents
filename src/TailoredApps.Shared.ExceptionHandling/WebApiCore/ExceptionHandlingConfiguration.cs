using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    /// <summary>
    /// Provides extension methods to register exception handling services for ASP.NET Core Web API projects.
    /// </summary>
    public static class ExceptionHandlingConfiguration
    {
        /// <summary>
        /// Registers exception handling services, the <see cref="HandleExceptionAttribute"/> filter,
        /// and the specified exception-handling provider into the DI container.
        /// </summary>
        /// <typeparam name="TExceptionHandlingProviderInterface">
        /// The interface type of the exception handling provider.
        /// </typeparam>
        /// <typeparam name="TTExceptionHandlingProvider">
        /// The concrete implementation type of the exception handling provider.
        /// </typeparam>
        /// <param name="service">The service collection to register into.</param>
        /// <returns>An <see cref="IExceptionHandlingOptionsBuilder"/> for further configuration.</returns>
        public static IExceptionHandlingOptionsBuilder AddExceptionHandlingForWebApi<TExceptionHandlingProviderInterface, TTExceptionHandlingProvider>(this IServiceCollection service)
            where TTExceptionHandlingProvider : class, TExceptionHandlingProviderInterface
            where TExceptionHandlingProviderInterface : class, IExceptionHandlingProvider
        {
            service.AddScoped<HandleExceptionAttribute>();
            return service.AddExceptionHandling<TExceptionHandlingProviderInterface, TTExceptionHandlingProvider>();
        }

        /// <summary>
        /// Adds <see cref="HandleExceptionFilterAttribute"/> as a global MVC filter so that all
        /// actions decorated with <see cref="HandleExceptionAttribute"/> are automatically handled.
        /// </summary>
        /// <param name="filter">The global filter collection.</param>
        public static void AddExceptionHAndlingFilterAttribute(this FilterCollection filter)
        {
            filter.Add<HandleExceptionFilterAttribute>();
        }
    }
}
