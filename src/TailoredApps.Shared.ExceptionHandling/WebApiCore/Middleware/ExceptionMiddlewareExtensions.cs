using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
                appError.Run(WriteErrorResponseAsync);
            });
        }

        /// <summary>
        /// Writes the structured error response for the exception stored in
        /// <see cref="IExceptionHandlerFeature"/>. Exposed for testing.
        /// </summary>
        /// <param name="context">The HTTP context of the failed request.</param>
        internal static async Task WriteErrorResponseAsync(HttpContext context)
        {
            if (context.Response.HasStarted)
            {
                // Headers already sent; nothing safe can be written any more.
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var contextFeature = context.Features.Get<IExceptionHandlerFeature>();

            if (contextFeature != null)
            {
                var exceptionHandlingService = context.RequestServices.GetRequiredService<IExceptionHandlingService>();
                var response = exceptionHandlingService.Response(contextFeature.Error);
                context.Response.StatusCode = NormalizeStatusCode(response.ErrorCode);
                await context.Response.WriteAsync(response.ToString());
            }
        }

        /// <summary>
        /// Only client (4xx) and server (5xx) codes are meaningful for an error response;
        /// anything else falls back to 500 so a mis-configured provider cannot produce a 2xx error.
        /// </summary>
        internal static int NormalizeStatusCode(int errorCode)
            => errorCode >= 400 && errorCode <= 599 ? errorCode : (int)HttpStatusCode.InternalServerError;
    }
}
