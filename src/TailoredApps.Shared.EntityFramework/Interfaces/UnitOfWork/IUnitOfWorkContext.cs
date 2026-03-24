using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    /// <summary>
    /// Provides low-level database operations used internally by the Unit of Work implementation:
    /// transaction management, change persistence, change discarding, and raw connection access.
    /// </summary>
    public interface IUnitOfWorkContext
    {
        /// <summary>
        /// Begins a new database transaction with the default isolation level.
        /// </summary>
        /// <returns>An <see cref="ITransaction"/> representing the open transaction.</returns>
        ITransaction BeginTransaction();

        /// <summary>
        /// Begins a new database transaction with the specified isolation level.
        /// </summary>
        /// <param name="isolationLevel">The isolation level for the transaction.</param>
        /// <returns>An <see cref="ITransaction"/> representing the open transaction.</returns>
        ITransaction BeginTransaction(IsolationLevel isolationLevel);

        /// <summary>
        /// Saves all pending changes in the current context to the database.
        /// </summary>
        /// <returns>The number of state entries written to the database.</returns>
        int SaveChanges();

        /// <summary>
        /// Asynchronously saves all pending changes in the current context to the database.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The number of state entries written to the database.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Detaches all tracked entities, effectively discarding any unsaved changes.
        /// </summary>
        void DiscardChanges();

        /// <summary>
        /// Returns the underlying <see cref="DbConnection"/> used by this context.
        /// </summary>
        /// <returns>The active database connection.</returns>
        DbConnection GetDbConnection();
    }
}
