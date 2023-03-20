using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        bool HasOpenTransaction { get; }

        /// <summary>
        /// Opens a new transaction if no transaction is currently open.
        /// This method should only be used if you need to open a transaction
        /// in one exact moment. Otherwise, let Unit of Work open it for you
        /// in a convenient time.
        /// </summary>
        void BeginTransactionManually();

        /// <summary>
        /// Commits the current transaction (if one is open)
        /// </summary>
        void CommitTransaction();

        /// <summary>
        /// Commits the current transaction (if one is open) and sets the isolation level for new ones.
        /// </summary>
        void CommitTransaction(IsolationLevel isolationLevel);

        /// <summary>
        /// Rolls back the current transaction (if one is open)
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        /// Rolls back the current transaction (if one is open) and sets the isolation level for new ones.
        /// </summary>
        void RollbackTransaction(IsolationLevel isolationLevel);

        /// <summary>
        /// Saves changes to database. If no transaction has been created yet,
        /// this method will open a new transaction with isolation level set in UoW
        /// before saving changes.
        /// </summary>
        int SaveChanges();

        /// <summary>
        /// Asynchronously saves changes to database. If no transaction has been created yet,
        /// this method will open a new transaction with isolation level set in UoW
        /// before saving changes.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets the isolation level for new transactions. This method does not
        /// change the isolation level of the currently open transaction.
        /// </summary>
        /// <param name="isolationLevel"></param>
        void SetIsolationLevel(IsolationLevel isolationLevel);
    }

    public interface IUnitOfWork<T> : IUnitOfWork
    {
        T DataProvider { get; }
    }
}