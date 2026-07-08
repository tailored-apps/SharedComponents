namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    /// <summary>
    /// Manages and executes the registered Unit of Work lifecycle hooks
    /// at the appropriate points in the save/transaction cycle.
    /// </summary>
    public interface IHooksManager
    {
        /// <summary>
        /// Executes all registered <see cref="IPreSaveChangesHook"/> implementations
        /// before changes are saved to the database.
        /// </summary>
        void ExecutePreSaveChangesHooks();

        /// <summary>
        /// Executes all registered <see cref="IPostSaveChangesHook"/> implementations
        /// after changes have been saved to the database.
        /// </summary>
        void ExecutePostSaveChangesHooks();

        /// <summary>
        /// Executes all registered <see cref="ITransactionRollbackHook"/> implementations
        /// after a transaction has been rolled back.
        /// </summary>
        void ExecuteTransactionRollbackHooks();

        /// <summary>
        /// Executes all registered <see cref="ITransactionCommitHook"/> implementations
        /// after a transaction has been committed.
        /// </summary>
        void ExecuteTransactionCommitHooks();
    }
}
