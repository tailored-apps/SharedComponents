namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    /// <summary>
    /// Defines the lifecycle contract for the Unit of Work audit context.
    /// Manages collection, post-processing, discarding, and final auditing of entity changes.
    /// </summary>
    public interface IUnitOfWorkAuditContext
    {
        /// <summary>
        /// Performs any post-collection processing on the gathered entity changes
        /// (e.g. enriching change records after save).
        /// </summary>
        void PostCollectChanges();

        /// <summary>
        /// Collects the current entity changes from the EF Core change tracker
        /// before the save operation is committed.
        /// </summary>
        void CollectChanges();

        /// <summary>
        /// Discards all previously collected entity changes, typically called on transaction rollback.
        /// </summary>
        void DiscardChanges();

        /// <summary>
        /// Passes the collected entity changes to the registered <see cref="IEntityChangesAuditor"/>
        /// for processing and persistence (e.g. after a successful transaction commit).
        /// </summary>
        void AuditChanges();
    }
}
