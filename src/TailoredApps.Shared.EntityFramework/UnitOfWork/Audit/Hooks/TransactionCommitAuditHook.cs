using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks
{
    internal class TransactionCommitAuditHook : ITransactionCommitHook
    {
        private readonly IUnitOfWorkAuditContext _unitOfWorkAudit;

        public TransactionCommitAuditHook(IUnitOfWorkAuditContext unitOfWorkAudit)
        {
            _unitOfWorkAudit = unitOfWorkAudit;
        }

        public void Execute()
            => _unitOfWorkAudit.AuditChanges();
    }
}