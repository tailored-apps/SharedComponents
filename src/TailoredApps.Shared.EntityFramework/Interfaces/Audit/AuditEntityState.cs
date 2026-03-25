namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    /// <summary>
    /// Represents the audited state of an entity tracked by the Unit of Work audit context.
    /// Mirrors the relevant values of <see cref="Microsoft.EntityFrameworkCore.EntityState"/>.
    /// </summary>
    public enum AuditEntityState
    {
        /// <summary>
        /// Microsoft.EntityFrameworkCore.EntityState.Added
        /// </summary>
        Added,

        /// <summary>
        /// Microsoft.EntityFrameworkCore.EntityState.Modified
        /// </summary>
        Modified,

        /// <summary>
        /// Microsoft.EntityFrameworkCore.EntityState.Deleted
        /// </summary>
        Deleted,
    }
}
