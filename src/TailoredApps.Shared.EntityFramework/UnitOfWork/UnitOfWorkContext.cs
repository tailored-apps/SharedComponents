using Microsoft.EntityFrameworkCore;

using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{

    public class UnitOfWorkContext<T> : IUnitOfWorkContext where T : DbContext
    {
        private readonly T _dbContext;

        public UnitOfWorkContext(T dbContext)
        {
            _dbContext = dbContext;
        }

        public DbConnection GetDbConnection()
        {
            if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                return new InMemoryDbConnection();

            return _dbContext.Database.GetDbConnection();
        }

        public ITransaction BeginTransaction()
        {
            var dbTransaction = _dbContext.Database.BeginTransaction();

            return new Transaction(dbTransaction);
        }

        public ITransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            var dbTransaction = _dbContext.Database.BeginTransaction(isolationLevel);

            return new Transaction(dbTransaction);
        }

        public int SaveChanges()
        {
            return _dbContext.SaveChanges();
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public void DiscardChanges()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}