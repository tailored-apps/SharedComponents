using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    /// <summary>
    /// Fluent builder for configuring Unit of Work lifecycle hooks during application startup.
    /// </summary>
    public interface IUnitOfWorkOptionsBuilder
    {
        /// <summary>
        /// Registers a <see cref="ITransactionCommitHook"/> implementation (resolved via DI).
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>() where THook : class, ITransactionCommitHook;

        /// <summary>
        /// Registers a <see cref="ITransactionCommitHook"/> implementation using a factory delegate.
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <param name="implementationFactory">A factory that creates the hook instance.</param>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithTransactionCommitHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionCommitHook;

        /// <summary>
        /// Registers a <see cref="ITransactionRollbackHook"/> implementation (resolved via DI).
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>() where THook : class, ITransactionRollbackHook;

        /// <summary>
        /// Registers a <see cref="ITransactionRollbackHook"/> implementation using a factory delegate.
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <param name="implementationFactory">A factory that creates the hook instance.</param>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithTransactionRollbackHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, ITransactionRollbackHook;

        /// <summary>
        /// Registers a <see cref="IPreSaveChangesHook"/> implementation (resolved via DI).
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>() where THook : class, IPreSaveChangesHook;

        /// <summary>
        /// Registers a <see cref="IPreSaveChangesHook"/> implementation using a factory delegate.
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <param name="implementationFactory">A factory that creates the hook instance.</param>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithPreSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPreSaveChangesHook;

        /// <summary>
        /// Registers a <see cref="IPostSaveChangesHook"/> implementation (resolved via DI).
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>() where THook : class, IPostSaveChangesHook;

        /// <summary>
        /// Registers a <see cref="IPostSaveChangesHook"/> implementation using a factory delegate.
        /// </summary>
        /// <typeparam name="THook">The hook implementation type.</typeparam>
        /// <param name="implementationFactory">A factory that creates the hook instance.</param>
        /// <returns>The current builder for further chaining.</returns>
        IUnitOfWorkOptionsBuilder WithPostSaveChangesHook<THook>(Func<IServiceProvider, THook> implementationFactory) where THook : class, IPostSaveChangesHook;

        /// <summary>
        /// Gets the underlying <see cref="IServiceCollection"/> used for DI registrations.
        /// </summary>
        IServiceCollection Services { get; }
    }
}
