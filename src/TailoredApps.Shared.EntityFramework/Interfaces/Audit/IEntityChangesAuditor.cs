using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    /// <summary>
    /// Defines the contract for processing and persisting a collection of audited entity changes
    /// after they have been collected by the Unit of Work audit context.
    /// </summary>
    public interface IEntityChangesAuditor
    {
        /// <summary>
        /// Processes and stores the given entity changes (e.g. writes them to an audit log).
        /// </summary>
        /// <param name="entityChanges">The collection of entity changes to audit.</param>
        void AuditChanges(IEnumerable<EntityChange> entityChanges);
    }
}
