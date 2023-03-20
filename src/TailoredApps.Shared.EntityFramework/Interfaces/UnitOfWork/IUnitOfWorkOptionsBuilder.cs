using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{

    public interface IUnitOfWorkOptionsBuilder
    {
        IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>() where THook : class, ITransactionCommitHook;
        IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionCommitHook;

        IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>() where THook : class, ITransactionRollbackHook;
        IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionRollbackHook;

        IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>() where THook : class, IPreSaveChangesHook;
        IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPreSaveChangesHook;

        IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>() where THook : class, IPostSaveChangesHook;
        IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPostSaveChangesHook;

        IServiceCollection Services { get; }
    }
}