using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Net;
using TailoredApps.Shared.ExceptionHandling.Interfaces;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware
{
    public static class ExceptionMiddlewareExtensions
    {
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
