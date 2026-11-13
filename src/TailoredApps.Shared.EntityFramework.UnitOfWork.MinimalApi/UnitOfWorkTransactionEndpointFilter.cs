using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

/// <summary>
/// Endpoint filter that runs a minimal-API endpoint inside the request's unit of work: the changes are saved
/// and the transaction committed after the endpoint succeeds, and rolled back after an exception or after a
/// result with a status of 400 or more. It is the minimal-API counterpart of the MVC transaction filter of
/// TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore, with the same rules.
/// </summary>
/// <remarks>
/// <para>
/// An endpoint that returns a plain object, or a result without a status code, counts as a success.
/// </para>
/// <para>
/// When the commit fails, the transaction is rolled back and the commit exception is thrown; when the rollback
/// fails as well, both are thrown together in an <see cref="AggregateException"/>, so the original cause stays
/// visible.
/// </para>
/// </remarks>
public sealed class UnitOfWorkTransactionEndpointFilter : IEndpointFilter
{
    private readonly IsolationLevel? isolationLevel;

    /// <summary>
    /// Initializes a new instance of <see cref="UnitOfWorkTransactionEndpointFilter"/>.
    /// </summary>
    /// <param name="isolationLevel">
    /// The isolation level of the transaction for the endpoints this filter is added to, or <c>null</c> for the
    /// unit of work's default.
    /// </param>
    public UnitOfWorkTransactionEndpointFilter(IsolationLevel? isolationLevel = null)
    {
        this.isolationLevel = isolationLevel;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        if (isolationLevel is { } level)
            unitOfWork.SetIsolationLevel(level);

        object? result;
        try
        {
            result = await next(context);
        }
        catch (Exception endpointException)
        {
            Rollback(unitOfWork, endpointException);
            throw;
        }

        if (IsErrorResult(result))
        {
            unitOfWork.RollbackTransaction();
            return result;
        }

        try
        {
            unitOfWork.CommitTransaction();
        }
        catch (Exception commitException)
        {
            Rollback(unitOfWork, commitException);
            throw;
        }

        return result;
    }

    /// <summary>A result that carries a status of 400 or more is an error: the endpoint refused or failed.</summary>
    internal static bool IsErrorResult(object? result)
        => result is IStatusCodeHttpResult { StatusCode: >= 400 };

    /// <summary>
    /// Rolls back after <paramref name="cause"/>. A failing rollback must not hide the exception that led to
    /// it, so both are reported together.
    /// </summary>
    private static void Rollback(IUnitOfWork unitOfWork, Exception cause)
    {
        try
        {
            unitOfWork.RollbackTransaction();
        }
        catch (Exception rollbackException)
        {
            throw new AggregateException(cause, rollbackException);
        }
    }
}
