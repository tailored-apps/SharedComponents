using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks
{
    internal class PostSaveChangesAuditHook : IPostSaveChangesHook
    {
        private readonly IUnitOfWorkAuditContext _unitOfWorkAudit;

        public PostSaveChangesAuditHook(IUnitOfWorkAuditContext unitOfWorkAudit)
        {
            _unitOfWorkAudit = unitOfWorkAudit;
        }

        public void Execute()
            => _unitOfWorkAudit.PostCollectChanges();
    }
}