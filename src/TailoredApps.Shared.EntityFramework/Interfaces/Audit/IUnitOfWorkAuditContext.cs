namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    public interface IUnitOfWorkAuditContext
    {
        void PostCollectChanges();
        void CollectChanges();
        void DiscardChanges();
        void AuditChanges();
    }
}