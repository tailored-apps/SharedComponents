using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks
{
    internal class PreSaveChangesAuditHook : IPreSaveChangesHook
    {
        private readonly IUnitOfWorkAuditContext _unitOfWorkAudit;

        public PreSaveChangesAuditHook(IUnitOfWorkAuditContext unitOfWorkAudit)
        {
            _unitOfWorkAudit = unitOfWorkAudit;
        }

        public void Execute()
            => _unitOfWorkAudit.CollectChanges();
    }
}