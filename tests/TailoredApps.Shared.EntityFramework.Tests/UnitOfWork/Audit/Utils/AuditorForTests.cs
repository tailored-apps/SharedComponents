using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils
{
    public class AuditorForTests : IEntityChangesAuditor
    {
        public void AuditChanges(IEnumerable<EntityChange> entityChanges) { }
    }
}
