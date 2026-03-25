using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore
{
    /// <summary>
    /// Extension methods for registering the Unit of Work pattern in ASP.NET Core Web API projects.
    /// </summary>
    public static class UnitOfWorkConfiguration
    {
        /// <summary>
        /// Registers the Unit of Work and the <see cref="TransactionFilterAttribute"/> in the DI container,
        /// scoped to the current HTTP request.
        /// </summary>
        /// <typeparam name="TTargetDbContextInterface">The interface that <typeparamref name="TTargetDbContext"/> must implement.</typeparam>
        /// <typeparam name="TTargetDbContext">The concrete EF Core <see cref="DbContext"/> type.</typeparam>
        /// <param name="services">The DI service collection to configure.</param>
        /// <returns>An <see cref="IUnitOfWorkOptionsBuilder"/> for further configuration (hooks, auditing, etc.).</returns>
        public static IUnitOfWorkOptionsBuilder AddUnitOfWorkForWebApi<TTargetDbContextInterface, TTargetDbContext>(this IServiceCollection services)
            where TTargetDbContext : DbContext, TTargetDbContextInterface
            where TTargetDbContextInterface : class
        {
            services.AddScoped<TransactionFilterAttribute>();
            return services.AddUnitOfWork<TTargetDbContextInterface, TTargetDbContext>();
        }

        /// <summary>
        /// Adds <see cref="TransactionFilterAttribute"/> as a global MVC filter so that every
        /// controller action is automatically wrapped in a Unit of Work transaction.
        /// </summary>
        /// <param name="filters">The application's global filter collection.</param>
        public static void AddUnitOfWorkTransactionAttribute(this FilterCollection filters)
        {
            filters.Add<TransactionFilterAttribute>();
        }
    }
}
