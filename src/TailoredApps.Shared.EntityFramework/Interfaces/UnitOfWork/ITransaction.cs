using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    /// <summary>
    /// Represents an active database transaction managed by the Unit of Work.
    /// Provides commit and rollback operations and must be disposed when no longer needed.
    /// </summary>
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// Commits all changes made within this transaction to the database.
        /// </summary>
        void Commit();

        /// <summary>
        /// Rolls back all changes made within this transaction, discarding any pending modifications.
        /// </summary>
        void Rollback();
    }
}
