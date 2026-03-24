using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    /// <summary>
    /// Marks an entity as tracking activity metadata: who created or last modified it and when.
    /// </summary>
    public interface IActivity
    {
        /// <summary>
        /// Gets or sets the UTC date and time when the entity was created.
        /// </summary>
        DateTime CreatedDateUtc { get; set; }

        /// <summary>
        /// Gets or sets the identifier (e.g. username or email) of the user who created the entity.
        /// </summary>
        string CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the UTC date and time when the entity was last modified, or <c>null</c> if it has never been modified.
        /// </summary>
        DateTime? ModifiedDateUtc { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who last modified the entity, or <c>null</c> if it has never been modified.
        /// </summary>
        string ModifiedBy { get; set; }
    }
}
