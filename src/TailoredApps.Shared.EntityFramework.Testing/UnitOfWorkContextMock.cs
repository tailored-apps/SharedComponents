using Microsoft.EntityFrameworkCore;
using Moq;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.Testing
{

    public class UnitOfWorkContextMock<T> : IUnitOfWorkContext where T : DbContext
    {
        private T _dbContext;

        public UnitOfWorkContextMock(T dbContext)
        {
            _dbContext = dbContext;
        }

        public ITransaction BeginTransaction()
        {
            return new Mock<ITransaction>().Object;
        }

        public ITransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            return new Mock<ITransaction>().Object;
        }

        public int SaveChanges()
        {
            var result = _dbContext.SaveChanges();

            return result;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync(new CancellationToken());
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
        }

        public DbConnection GetDbConnection()
        {
            return new Mock<DbConnection>().Object;
        }

        public void DiscardChanges()
        {

        }

    }
}
