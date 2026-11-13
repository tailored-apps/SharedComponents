using System;
using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

/// <summary>
/// Registration and endpoint conventions of the unit of work for minimal APIs.
/// </summary>
public static class UnitOfWorkMinimalApiExtensions
{
    /// <summary>
    /// Registers the unit of work over <typeparamref name="TTargetDbContext"/> for a minimal-API application.
    /// It registers what <c>AddUnitOfWork</c> of TailoredApps.Shared.EntityFramework does, under the name a
    /// minimal-API application looks for; the transaction is applied per endpoint or route group with
    /// <see cref="WithUnitOfWorkTransaction{TBuilder}"/>.
    /// </summary>
    /// <typeparam name="TTargetDbContextInterface">The interface the application sees the context through.</typeparam>
    /// <typeparam name="TTargetDbContext">The Entity Framework context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>A builder for registering hooks and auditing.</returns>
    public static IUnitOfWorkOptionsBuilder AddUnitOfWorkForMinimalApi<TTargetDbContextInterface, TTargetDbContext>(this IServiceCollection services)
        where TTargetDbContext : DbContext, TTargetDbContextInterface
        where TTargetDbContextInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddUnitOfWork<TTargetDbContextInterface, TTargetDbContext>();
    }

    /// <summary>
    /// Runs the endpoint, or every endpoint of the group, inside the request's unit of work: commit after a
    /// success, rollback after an exception or a result with a status of 400 or more. The endpoint does not
    /// call <c>SaveChanges</c>. See <see cref="UnitOfWorkTransactionEndpointFilter"/>.
    /// </summary>
    /// <remarks>
    /// Filters run in the order they are added. A filter added before this one sees the outcome after the
    /// transaction has ended; a filter added after it runs inside the transaction.
    /// </remarks>
    /// <typeparam name="TBuilder">A route handler builder or a route group builder.</typeparam>
    /// <param name="builder">The endpoint or the route group.</param>
    /// <param name="isolationLevel">The isolation level of the transaction, or <c>null</c> for the default.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static TBuilder WithUnitOfWorkTransaction<TBuilder>(this TBuilder builder, IsolationLevel? isolationLevel = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new UnitOfWorkTransactionEndpointFilter(isolationLevel));
    }
}
