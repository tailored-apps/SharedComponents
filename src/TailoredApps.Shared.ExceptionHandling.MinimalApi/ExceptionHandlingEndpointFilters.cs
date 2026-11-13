using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.MinimalApi;

/// <summary>
/// Turns the error model of TailoredApps.Shared.ExceptionHandling into a minimal-API result.
/// </summary>
public static class ExceptionHandlingResults
{
    /// <summary>
    /// Creates a result that answers with <paramref name="model"/> as JSON and with its
    /// <see cref="ExceptionHandlingResultModel.ErrorCode"/> as the HTTP status. The JSON is the one the
    /// exception middleware and the MVC filter write, so a client sees one error shape everywhere.
    /// </summary>
    /// <param name="model">The error model, usually built by an <see cref="IExceptionHandlingProvider"/>.</param>
    /// <returns>A result with a status between 400 and 599; a code outside that range becomes 500.</returns>
    public static IResult ToResult(this ExceptionHandlingResultModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Results.Content(model.ToString(), "application/json", Encoding.UTF8, NormalizeStatusCode(model.ErrorCode));
    }

    /// <summary>
    /// Only client (4xx) and server (5xx) codes make sense for an error response; anything else falls back to
    /// 500, so a mis-configured provider cannot produce a 2xx error.
    /// </summary>
    internal static int NormalizeStatusCode(int errorCode)
        => errorCode is >= 400 and <= 599 ? errorCode : (int)HttpStatusCode.InternalServerError;
}

/// <summary>
/// Endpoint filter that answers an exception thrown by the endpoint (or by a filter after this one) with the
/// error model of the registered <see cref="IExceptionHandlingProvider"/>. It is the minimal-API counterpart
/// of handling exceptions per controller: use it on an endpoint or a route group when only a part of the
/// application should answer this way. For the whole application the middleware
/// (<c>app.ConfigureExceptionHandler()</c>) is enough and this filter is not needed.
/// </summary>
/// <remarks>
/// An exception answered with a 4xx status is the client's mistake and is logged at Debug level; one answered
/// with 5xx is logged as an error. A cancelled request is not handled: <see cref="OperationCanceledException"/>
/// passes through.
/// </remarks>
public sealed class ExceptionHandlingEndpointFilter : IEndpointFilter
{
    /// <summary>The log category of the entries this filter writes.</summary>
    internal const string LogCategory = "TailoredApps.Shared.ExceptionHandling";

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var services = context.HttpContext.RequestServices;
            var model = services.GetRequiredService<IExceptionHandlingService>().Response(exception);
            var status = ExceptionHandlingResults.NormalizeStatusCode(model.ErrorCode);

            var logger = services.GetService<ILoggerFactory>()?.CreateLogger(LogCategory);
            if (status < 500)
                logger?.LogDebug(exception, "{ExceptionType} answered with {StatusCode}.", exception.GetType().Name, status);
            else
                logger?.LogError(exception, "{ExceptionType} answered with {StatusCode}.", exception.GetType().Name, status);

            return model.ToResult();
        }
    }
}

/// <summary>
/// Endpoint filter that validates the argument of type <typeparamref name="TRequest"/> with every
/// <see cref="IValidator{T}"/> registered for it, before the endpoint runs. A failure is answered with the
/// error model of the registered <see cref="IExceptionHandlingProvider"/> - by default 400 with one error per
/// invalid property - and the endpoint is not called.
/// </summary>
/// <remarks>
/// The request type is a type parameter on purpose: the validators are resolved by their closed generic type,
/// with no reflection over the endpoint's arguments, which keeps the filter usable under NativeAOT.
/// An endpoint without an argument of this type, or without a registered validator, is passed through.
/// </remarks>
/// <typeparam name="TRequest">The type of the endpoint argument to validate.</typeparam>
public sealed class ValidationEndpointFilter<TRequest> : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return await next(context);

        var services = context.HttpContext.RequestServices;
        List<ValidationFailure>? failures = null;
        foreach (var validator in services.GetServices<IValidator<TRequest>>())
        {
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid)
                (failures ??= []).AddRange(result.Errors);
        }

        if (failures is null)
            return await next(context);

        return services.GetRequiredService<IExceptionHandlingService>()
            .Response(new ValidationException(failures))
            .ToResult();
    }
}
