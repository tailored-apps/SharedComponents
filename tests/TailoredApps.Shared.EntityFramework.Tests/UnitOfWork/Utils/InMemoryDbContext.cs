using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Utils
{
    public interface IExampleDbContext { }

    public class InMemoryDbContext : DbContext, IExampleDbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Create a fresh service provider, and therefore a fresh 
            // InMemory database instance.
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            optionsBuilder.UseInMemoryDatabase($"{Guid.NewGuid()}")
                .UseInternalServiceProvider(serviceProvider);
        }

        private DatabaseFacade _database = null;

        public override DatabaseFacade Database
        {
            get
            {
                if (_database != null) return _database;

                var transactionMock = new Moq.Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();

                var databaseFacadeMock = new Moq.Mock<DatabaseFacade>(this);
                databaseFacadeMock.Setup(x => x.BeginTransaction()).Returns(transactionMock.Object);

                _database = databaseFacadeMock.Object;

                return _database;
            }
        }
    }
}
