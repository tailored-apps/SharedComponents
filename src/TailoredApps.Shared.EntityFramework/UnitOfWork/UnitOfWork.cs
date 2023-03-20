using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    public class UnitOfWork<T> : IUnitOfWork<T>
    {
        private readonly IUnitOfWorkContext _context;
        private readonly IHooksManager _hooksManager;
        private ITransaction _transaction;
        private IsolationLevel _isolationLevel;

        public T DataProvider { get; }

        public bool HasOpenTransaction => _transaction != null;

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

        public void BeginTransactionManually()
        {
            StartNewTransactionIfNeeded();
        }

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

        public void CommitTransaction(IsolationLevel isolationLevel)
        {
            CommitTransaction();

            _isolationLevel = isolationLevel;
        }

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

        public void RollbackTransaction(IsolationLevel isolationLevel)
        {
            RollbackTransaction();

            _isolationLevel = isolationLevel;
        }

        public int SaveChanges()
        {
            StartNewTransactionIfNeeded();

            _hooksManager.ExecutePreSaveChangesHooks();

            var result = _context.SaveChanges();

            _hooksManager.ExecutePostSaveChangesHooks();

            return result;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            StartNewTransactionIfNeeded();

            _hooksManager.ExecutePreSaveChangesHooks();

            var result = await _context.SaveChangesAsync(cancellationToken);

            _hooksManager.ExecutePostSaveChangesHooks();

            return result;
        }

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

        public void Dispose()
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }
}