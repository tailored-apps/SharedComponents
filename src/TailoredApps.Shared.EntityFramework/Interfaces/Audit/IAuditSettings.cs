using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    public interface IAuditSettings
    {
        IEnumerable<Type> TypesToCollect { get; set; }

        IEnumerable<AuditEntityState> EntityStatesToCollect { get; set; }
    }

}