using Microsoft.EntityFrameworkCore.Storage;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    /// <summary>
    /// Wraps an EF Core <see cref="IDbContextTransaction"/> and exposes it through the
    /// <see cref="ITransaction"/> interface used by the Unit of Work layer.
    /// </summary>
    public class Transaction : ITransaction
    {
        private readonly IDbContextTransaction _transaction;

        /// <summary>
        /// Initializes a new instance of <see cref="Transaction"/>.
        /// </summary>
        /// <param name="transaction">The underlying EF Core database transaction to wrap.</param>
        public Transaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <summary>
        /// Commits all changes made within this transaction to the database.
        /// </summary>
        public void Commit()
        {
            _transaction.Commit();
        }

        /// <summary>
        /// Rolls back all changes made within this transaction, discarding any pending modifications.
        /// </summary>
        public void Rollback()
        {
            _transaction.Rollback();
        }

        /// <summary>
        /// Disposes the underlying EF Core transaction.
        /// </summary>
        public void Dispose()
        {
            _transaction.Dispose();
        }
    }
}
