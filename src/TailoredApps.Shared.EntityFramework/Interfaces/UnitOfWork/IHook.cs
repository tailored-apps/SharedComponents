

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    /// <summary>
    /// Base contract for Unit of Work lifecycle hooks.
    /// Implementations are invoked at specific points in the save/transaction lifecycle.
    /// </summary>
    public interface IHook
    {
        /// <summary>
        /// Executes the hook logic at the appropriate lifecycle point.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Hook that is executed immediately after a transaction is successfully committed.
    /// </summary>
    public interface ITransactionCommitHook : IHook { }

    /// <summary>
    /// Hook that is executed immediately after a transaction is rolled back.
    /// </summary>
    public interface ITransactionRollbackHook : IHook { }

    /// <summary>
    /// Hook that is executed immediately after <c>SaveChanges</c> completes successfully.
    /// </summary>
    public interface IPostSaveChangesHook : IHook { }

    /// <summary>
    /// Hook that is executed immediately before <c>SaveChanges</c> is called.
    /// </summary>
    public interface IPreSaveChangesHook : IHook { }
}
