using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Net;
using TailoredApps.Shared.ExceptionHandling.Interfaces;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware
{
    /// <summary>
    /// Provides extension methods for registering the global exception-handling middleware
    /// into the ASP.NET Core request pipeline.
    /// </summary>
    public static class ExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Registers a global exception handler that catches unhandled exceptions, converts them
        /// to a structured JSON response using the registered <see cref="IExceptionHandlingService"/>,
        /// and writes the appropriate HTTP status code.
        /// </summary>
        /// <param name="app">The application builder to add the exception handler to.</param>
        public static void ConfigureExceptionHandler(this IApplicationBuilder app)
        {
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";
                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();

                    if (contextFeature != null)
                    {
                        var exceptionHandlingService = context.RequestServices.GetService(typeof(IExceptionHandlingService)) as IExceptionHandlingService;
                        var response = exceptionHandlingService.Response(contextFeature.Error);
                        context.Response.StatusCode = response.ErrorCode;
                        await context.Response.WriteAsync(response.ToString());
                    }
                });
            });
        }
    }
}
