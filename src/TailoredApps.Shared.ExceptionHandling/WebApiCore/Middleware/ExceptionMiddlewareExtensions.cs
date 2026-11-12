using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.ExceptionHandling.Interfaces;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;

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
    /// <remarks>
    /// An exception the provider answers with a client error (4xx: a failed validation, a missing resource)
    /// is the client's mistake, not a failure of the application. For those the middleware's diagnostics are
    /// suppressed - no "unhandled exception" error in the log and no <c>error.type</c> tag on the request
    /// duration metric - and the exception is logged at Debug level instead. A 5xx answer keeps the
    /// middleware's error log. A 404 chosen by the provider is answered as such.
    /// </remarks>
    /// <param name="app">The application builder to add the exception handler to.</param>
    public static void ConfigureExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = WriteErrorResponseAsync,
            // Without this the middleware takes a 404 for a misconfigured handler and rethrows the exception.
            AllowStatusCode404Response = true,
            // Runs after the handler, so the status is the one the provider chose.
            SuppressDiagnosticsCallback = context => IsClientError(context.HttpContext.Response.StatusCode),
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

            if (IsClientError(context.Response.StatusCode))
            {
                // The middleware does not log these (see ConfigureExceptionHandler); keep a trace for debugging.
                context.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger(LogCategory)
                    .LogDebug(contextFeature.Error, "{ExceptionType} answered with {StatusCode}.", contextFeature.Error.GetType().Name, context.Response.StatusCode);
            }

            await context.Response.WriteAsync(response.ToString());
        }
    }

    /// <summary>The log category of the Debug entries written for client errors.</summary>
    internal const string LogCategory = "TailoredApps.Shared.ExceptionHandling";

    internal static bool IsClientError(int statusCode) => statusCode is >= 400 and < 500;

    /// <summary>
    /// Only client (4xx) and server (5xx) codes are meaningful for an error response;
    /// anything else falls back to 500 so a mis-configured provider cannot produce a 2xx error.
    /// </summary>
    internal static int NormalizeStatusCode(int errorCode)
        => errorCode >= 400 && errorCode <= 599 ? errorCode : (int)HttpStatusCode.InternalServerError;
}
