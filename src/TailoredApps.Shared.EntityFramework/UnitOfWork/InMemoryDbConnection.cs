using System;
using System.Data;
using System.Data.Common;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork
{

    internal class InMemoryDbConnection : DbConnection
    {
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        public override void Close()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        public override void Open()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        public override string ConnectionString
        {
            get => throw new NotSupportedException("Not supported by InMemory DbContext");
            set => throw new NotSupportedException("Not supported by InMemory DbContext");
        }

        public override string Database => throw new NotSupportedException("Not supported by InMemory DbContext");
        public override ConnectionState State => throw new NotSupportedException("Not supported by InMemory DbContext");
        public override string DataSource => throw new NotSupportedException("Not supported by InMemory DbContext");
        public override string ServerVersion => throw new NotSupportedException("Not supported by InMemory DbContext");

        protected override DbCommand CreateDbCommand()
        {
            throw new NotSupportedException("Not supported by InMemory DbContext");
        }
    }
}