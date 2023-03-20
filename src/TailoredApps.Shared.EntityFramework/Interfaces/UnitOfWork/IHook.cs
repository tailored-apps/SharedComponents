

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    public interface IHook
    {
        void Execute();
    }

    public interface ITransactionCommitHook : IHook { }

    public interface ITransactionRollbackHook : IHook { }

    public interface IPostSaveChangesHook : IHook { }

    public interface IPreSaveChangesHook : IHook { }
}