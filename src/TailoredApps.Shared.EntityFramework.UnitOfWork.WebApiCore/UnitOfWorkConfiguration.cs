using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore
{
    public static class UnitOfWorkConfiguration
    {
        public static IUnitOfWorkOptionsBuilder AddUnitOfWorkForWebApi<TTargetDbContextInterface, TTargetDbContext>(this IServiceCollection services)
            where TTargetDbContext : DbContext, TTargetDbContextInterface
            where TTargetDbContextInterface : class
        {
            services.AddScoped<TransactionFilterAttribute>();
            return services.AddUnitOfWork<TTargetDbContextInterface, TTargetDbContext>();
        }

        public static void AddUnitOfWorkTransactionAttribute(this FilterCollection filters)
        {
            filters.Add<TransactionFilterAttribute>();
        }
    }
}