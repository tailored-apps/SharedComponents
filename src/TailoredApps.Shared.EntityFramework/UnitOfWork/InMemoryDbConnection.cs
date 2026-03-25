using System;
using System.Data;
using System.Data.Common;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{
    /// <summary>
    /// A stub <see cref="DbConnection"/> used as a placeholder when the EF Core InMemory provider is active.
    /// All operations throw <see cref="NotSupportedException"/> because in-memory databases do not
    /// expose a real underlying connection.
    /// </summary>
    internal class InMemoryDbConnection : DbConnection
    {
        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void Close()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void Open()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown on get or set.</exception>
        public override string ConnectionString
        {
            get => throw new NotSupportedException("Not supported by InMemory DbContext");
            set => throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override string Database => throw new NotSupportedException("Not supported by InMemory DbContext");

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override ConnectionState State => throw new NotSupportedException("Not supported by InMemory DbContext");

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override string DataSource => throw new NotSupportedException("Not supported by InMemory DbContext");

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override string ServerVersion => throw new NotSupportedException("Not supported by InMemory DbContext");

        /// <summary>
        /// Not supported by the InMemory provider.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        protected override DbCommand CreateDbCommand()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }
    }
}
