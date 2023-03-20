using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    public interface IEntityChangesAuditor
    {
        void AuditChanges(IEnumerable<EntityChange> entityChanges);
    }
}