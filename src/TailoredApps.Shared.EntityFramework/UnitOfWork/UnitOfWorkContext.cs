using Microsoft.EntityFrameworkCore;

using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    /// <summary>
    /// EF Core implementation of <see cref="IUnitOfWorkContext"/> that wraps a
    /// <typeparamref name="T"/> <see cref="DbContext"/> instance.
    /// Provides transaction management, save operations, change discarding, and connection access.
    /// </summary>
    /// <typeparam name="T">The EF Core DbContext type.</typeparam>
    public class UnitOfWorkContext<T> : IUnitOfWorkContext where T : DbContext
    {
        private readonly T _dbContext;

        /// <summary>
        /// Initializes a new instance of <see cref="UnitOfWorkContext{T}"/>.
        /// </summary>
        /// <param name="dbContext">The EF Core DbContext to wrap.</param>
        public UnitOfWorkContext(T dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Returns the underlying <see cref="DbConnection"/> for the context.
        /// Returns an <see cref="InMemoryDbConnection"/> when the provider is the EF Core InMemory provider.
        /// </summary>
        /// <returns>The active or in-memory database connection.</returns>
        public DbConnection GetDbConnection()
        {
            if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                return new InMemoryDbConnection();

            return _dbContext.Database.GetDbConnection();
        }

        /// <summary>
        /// Begins a new database transaction with the default isolation level.
        /// </summary>
        /// <returns>An <see cref="ITransaction"/> wrapping the EF Core transaction.</returns>
        public ITransaction BeginTransaction()
        {
            var dbTransaction = _dbContext.Database.BeginTransaction();

            return new Transaction(dbTransaction);
        }

        /// <summary>
        /// Begins a new database transaction with the specified isolation level.
        /// </summary>
        /// <param name="isolationLevel">The isolation level for the transaction.</param>
        /// <returns>An <see cref="ITransaction"/> wrapping the EF Core transaction.</returns>
        public ITransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            var dbTransaction = _dbContext.Database.BeginTransaction(isolationLevel);

            return new Transaction(dbTransaction);
        }

        /// <summary>
        /// Saves all pending changes to the database via the underlying DbContext.
        /// </summary>
        /// <returns>The number of state entries written to the database.</returns>
        public int SaveChanges()
        {
            return _dbContext.SaveChanges();
        }

        /// <summary>
        /// Asynchronously saves all pending changes to the database via the underlying DbContext.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The number of state entries written to the database.</returns>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Detaches all tracked entities from the change tracker, discarding any unsaved modifications.
        /// </summary>
        public void DiscardChanges()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
