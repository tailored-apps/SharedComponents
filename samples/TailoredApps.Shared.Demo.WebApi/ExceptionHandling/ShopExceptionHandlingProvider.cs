using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;

namespace TailoredApps.Demo.WebApi.ExceptionHandling;

/// <summary>
/// The error bodies of the service. An exception the service threw on purpose (<see cref="ShopException"/>)
/// answers with its own status and message. Everything else keeps the defaults of ExceptionHandling: a
/// validation failure is a 400 with one error per field, an unexpected exception a 500 with a generic
/// message (the real one only in the Development environment and in the log).
/// </summary>
public class ShopExceptionHandlingProvider(IHostEnvironment environment) : IExceptionHandlingProvider
{
    private readonly DefaultExceptionHandlingProvider defaults = new(environment);

    public ExceptionHandlingResultModel Response(Exception exception)
    {
        // The outermost one: PaymentGatewayUnavailableException wraps the transport exception it was caused by.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ShopException shop)
                return new ExceptionHandlingResultModel(shop.StatusCode, shop.Message, [new ExceptionOrValidationError(string.Empty, shop.Message)]);
        }

        return defaults.Response(exception);
    }

    public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => defaults.Response(modelState);
}
