using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration
{
    internal sealed class AuditSettings : IAuditSettings
    {
        public IEnumerable<Type> TypesToCollect { get; set; } = Enumerable.Empty<Type>();
        public IEnumerable<AuditEntityState> EntityStatesToCollect { get; set; } = Enumerable.Empty<AuditEntityState>();
    }
}