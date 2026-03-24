using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    /// <summary>
    /// Defines the configuration settings that control which entity types and states
    /// are collected by the Unit of Work audit mechanism.
    /// </summary>
    public interface IAuditSettings
    {
        /// <summary>
        /// Gets or sets the collection of CLR types whose changes should be audited.
        /// Only entities whose type is present in this collection will be tracked.
        /// </summary>
        IEnumerable<Type> TypesToCollect { get; set; }

        /// <summary>
        /// Gets or sets the collection of entity states (Added, Modified, Deleted) to include
        /// in the audit. Only state transitions matching an entry in this collection are recorded.
        /// </summary>
        IEnumerable<AuditEntityState> EntityStatesToCollect { get; set; }
    }

}
