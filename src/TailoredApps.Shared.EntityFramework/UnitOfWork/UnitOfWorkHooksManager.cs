using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    internal class UnitOfWorkHooksManager : IHooksManager
    {
        private readonly IEnumerable<IHook> _hooks;

        public UnitOfWorkHooksManager(IEnumerable<IHook> hooks)
        {
            _hooks = hooks;
        }

        public void ExecutePreSaveChangesHooks()
            => ExecuteHooksOfType<IPreSaveChangesHook>();

        public void ExecutePostSaveChangesHooks()
            => ExecuteHooksOfType<IPostSaveChangesHook>();

        public void ExecuteTransactionRollbackHooks()
            => ExecuteHooksOfType<ITransactionRollbackHook>();

        public void ExecuteTransactionCommitHooks()
            => ExecuteHooksOfType<ITransactionCommitHook>();

        private void ExecuteHooksOfType<THookType>()
        {
            foreach (var hook in _hooks.Where(hook => hook is THookType))
                hook.Execute();
        }
    }
}