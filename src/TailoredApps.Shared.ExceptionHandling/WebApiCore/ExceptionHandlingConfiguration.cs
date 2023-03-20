using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    public static class ExceptionHandlingConfiguration
    {
        public static IExceptionHandlingOptionsBuilder AddExceptionHandlingForWebApi<TExceptionHandlingProviderInterface, TTExceptionHandlingProvider>(this IServiceCollection service)
            where TTExceptionHandlingProvider : class, TExceptionHandlingProviderInterface
            where TExceptionHandlingProviderInterface : class, IExceptionHandlingProvider
        {
            service.AddScoped<HandleExceptionAttribute>();
            return service.AddExceptionHandling<TExceptionHandlingProviderInterface, TTExceptionHandlingProvider>();
        }

        public static void AddExceptionHAndlingFilterAttribute(this FilterCollection filter)
        {
            filter.Add<HandleExceptionFilterAttribute>();
        }
    }
}
