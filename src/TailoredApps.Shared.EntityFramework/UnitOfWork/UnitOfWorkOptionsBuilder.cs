using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    internal class UnitOfWorkOptionsBuilder : IUnitOfWorkOptionsBuilder
    {
        public UnitOfWorkOptionsBuilder(IServiceCollection serviceCollection)
        {
            Services = serviceCollection;
        }

        public IServiceCollection Services { get; }

        public IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>() where THook : class, ITransactionCommitHook
            => WithHook<THook>();

        public IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionCommitHook
            => WithHook(implementationFactory);

        public IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>() where THook : class, ITransactionRollbackHook
            => WithHook<THook>();

        public IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionRollbackHook
            => WithHook(implementationFactory);

        public IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>() where THook : class, IPreSaveChangesHook
            => WithHook<THook>();

        public IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPreSaveChangesHook
            => WithHook(implementationFactory);

        public IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>() where THook : class, IPostSaveChangesHook
            => WithHook<THook>();

        public IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPostSaveChangesHook
            => WithHook(implementationFactory);

        private IUnitOfWorkOptionsBuilder WithHook<THook>() where THook : class, IHook
        {
            Services.AddTransient<IHook, THook>();
            return this;
        }

        private IUnitOfWorkOptionsBuilder WithHook<THook>(Func<IServiceProvider, THook> implementationFactory)
            where THook : class, IHook
        {
            Services.AddTransient<IHook, THook>(implementationFactory);
            return this;
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IUnitOfWorkOptionsBuilder AddUnitOfWork<TTargetDbContextInterface, TTargetDbContext>(this IServiceCollection services)
            where TTargetDbContext : DbContext, TTargetDbContextInterface
            where TTargetDbContextInterface : class
        {
            services.AddScoped<TTargetDbContextInterface, TTargetDbContext>(container => container.GetRequiredService<TTargetDbContext>());
            services.AddScoped<IUnitOfWorkContext, UnitOfWorkContext<TTargetDbContext>>();
            services.AddScoped<UnitOfWork<TTargetDbContextInterface>>();
            services.AddScoped<IUnitOfWork>(container => container.GetRequiredService<UnitOfWork<TTargetDbContextInterface>>());
            services.AddScoped<IUnitOfWork<TTargetDbContextInterface>>(container => container.GetRequiredService<UnitOfWork<TTargetDbContextInterface>>());
            services.AddTransient<IHooksManager, UnitOfWorkHooksManager>();

            return new UnitOfWorkOptionsBuilder(services);
        }
    }
}