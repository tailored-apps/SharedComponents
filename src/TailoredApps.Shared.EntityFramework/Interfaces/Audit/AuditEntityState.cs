namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{

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