namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    public interface IHooksManager
    {
        void ExecutePreSaveChangesHooks();
        void ExecutePostSaveChangesHooks();
        void ExecuteTransactionRollbackHooks();
        void ExecuteTransactionCommitHooks();
    }
}