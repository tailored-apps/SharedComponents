using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    /// <summary>
    /// Generic implementation of <see cref="IUnitOfWork{T}"/> that manages database transactions,
    /// lifecycle hooks, and change persistence for the provided data provider of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data provider (e.g. a DbContext interface).</typeparam>
    public class UnitOfWork<T> : IUnitOfWork<T>
    {
        private readonly IUnitOfWorkContext _context;
        private readonly IHooksManager _hooksManager;
        private ITransaction _transaction;
        private IsolationLevel _isolationLevel;

        /// <summary>
        /// Gets the underlying data provider (e.g. a repository or DbContext interface).
        /// </summary>
        public T DataProvider { get; }

        /// <summary>
        /// Gets a value indicating whether a database transaction is currently open.
        /// </summary>
        public bool HasOpenTransaction => _transaction != null;

        /// <summary>
        /// Initializes a new instance of <see cref="UnitOfWork{T}"/>.
        /// </summary>
        /// <param name="context">The low-level context used for transaction and save operations.</param>
        /// <param name="dataProvider">The typed data provider exposed to consumers.</param>
        /// <param name="hooksManager">The hooks manager used to invoke lifecycle hooks.</param>
        public UnitOfWork(IUnitOfWorkContext context, T dataProvider, IHooksManager hooksManager)
        {
            _context = context;
            _hooksManager = hooksManager;
            DataProvider = dataProvider;
            var dbConnection = _context.GetDbConnection();
            dbConnection.StateChange += OnStateChange;
            _isolationLevel = IsolationLevel.ReadCommitted;
        }

        private void StartNewTransactionIfNeeded()
        {
            if (_transaction == null)
            {
                _transaction = _context.BeginTransaction(_isolationLevel);
            }
        }

        /// <inheritdoc/>
        public void BeginTransactionManually()
        {
            StartNewTransactionIfNeeded();
        }

        /// <inheritdoc/>
        public void CommitTransaction()
        {
            SaveChanges();

            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction.Dispose();
                _transaction = null;
            }

            _hooksManager.ExecuteTransactionCommitHooks();
        }

        /// <inheritdoc/>
        public void CommitTransaction(IsolationLevel isolationLevel)
        {
            CommitTransaction();

            _isolationLevel = isolationLevel;
        }

        /// <inheritdoc/>
        public void RollbackTransaction()
        {
            _context.DiscardChanges();
            _hooksManager.ExecuteTransactionRollbackHooks();

            if (_transaction == null)
                return;

            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
        }

        /// <inheritdoc/>
        public void RollbackTransaction(IsolationLevel isolationLevel)
        {
            RollbackTransaction();

            _isolationLevel = isolationLevel;
        }

        /// <inheritdoc/>
        public int SaveChanges()
        {
            StartNewTransactionIfNeeded();

            _hooksManager.ExecutePreSaveChangesHooks();

            var result = _context.SaveChanges();

            _hooksManager.ExecutePostSaveChangesHooks();

            return result;
        }

        /// <inheritdoc/>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            StartNewTransactionIfNeeded();

            _hooksManager.ExecutePreSaveChangesHooks();

            var result = await _context.SaveChangesAsync(cancellationToken);

            _hooksManager.ExecutePostSaveChangesHooks();

            return result;
        }

        /// <inheritdoc/>
        public void SetIsolationLevel(IsolationLevel isolationLevel)
        {
            _isolationLevel = isolationLevel;
        }

        private void OnStateChange(object sender, StateChangeEventArgs args)
        {
            if (args.CurrentState == ConnectionState.Open && args.OriginalState != ConnectionState.Open)
            {
                var dbConnection = _context.GetDbConnection();
                using (var command = dbConnection.CreateCommand())
                {
                    switch (_isolationLevel)
                    {
                        case IsolationLevel.ReadCommitted:
                            command.CommandText = "SET TRANSACTION ISOLATION LEVEL READ COMMITTED";
                            break;
                        case IsolationLevel.ReadUncommitted:
                            command.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED";
                            break;
                        case IsolationLevel.Serializable:
                            command.CommandText = "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"The UoW library does not support isolation level {_isolationLevel}");
                    }
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Disposes the current transaction if one is open.
        /// </summary>
        public void Dispose()
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }
}
