using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    public interface IUnitOfWorkContext
    {
        ITransaction BeginTransaction();
        ITransaction BeginTransaction(IsolationLevel isolationLevel);

        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        void DiscardChanges();

        DbConnection GetDbConnection();
    }
}