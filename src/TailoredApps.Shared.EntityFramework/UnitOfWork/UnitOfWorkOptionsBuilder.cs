using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Provides extension methods on <see cref="IServiceCollection"/> for registering
    /// the Unit of Work infrastructure.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Unit of Work services for the specified DbContext and its interface,
        /// wiring up the context, hooks manager, and scoped UoW instances.
        /// </summary>
        /// <typeparam name="TTargetDbContextInterface">The interface type exposed to consumers.</typeparam>
        /// <typeparam name="TTargetDbContext">
        /// The concrete EF Core DbContext type that implements <typeparamref name="TTargetDbContextInterface"/>.
        /// </typeparam>
        /// <param name="services">The service collection to register into.</param>
        /// <returns>
        /// An <see cref="IUnitOfWorkOptionsBuilder"/> for registering lifecycle hooks and other options.
        /// </returns>
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
