using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks
{
    internal class TransactionRollbackAuditHook : ITransactionRollbackHook
    {
        private readonly IUnitOfWorkAuditContext _unitOfWorkAudit;

        public TransactionRollbackAuditHook(IUnitOfWorkAuditContext unitOfWorkAudit)
        {
            _unitOfWorkAudit = unitOfWorkAudit;
        }

        public void Execute()
            => _unitOfWorkAudit.DiscardChanges();
    }
}