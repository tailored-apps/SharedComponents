using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests
{
    public class UnitOfWorkConfigurationTests
    {
        public interface ITestDbContext
        {
        }

        public class TestDbContext : DbContext, ITestDbContext
        {
        }

        [Fact]
        public void When_AddUnitOfWorkForWebApi_Is_Called_Should_Register_TransactionFilterAttribute_As_Scoped()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddUnitOfWorkForWebApi<ITestDbContext, TestDbContext>();

            // assert
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TransactionFilterAttribute));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void When_AddUnitOfWorkForWebApi_Is_Called_Should_Register_UnitOfWork_Services()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddUnitOfWorkForWebApi<ITestDbContext, TestDbContext>();

            // assert
            Assert.Contains(services, d => d.ServiceType == typeof(ITestDbContext) && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d => d.ServiceType == typeof(IUnitOfWorkContext) && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d => d.ServiceType == typeof(IUnitOfWork) && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d => d.ServiceType == typeof(IUnitOfWork<ITestDbContext>) && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d => d.ServiceType == typeof(IHooksManager) && d.Lifetime == ServiceLifetime.Transient);
        }

        [Fact]
        public void When_AddUnitOfWorkForWebApi_Is_Called_Should_Return_Options_Builder_Wrapping_Same_Collection()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            var builder = services.AddUnitOfWorkForWebApi<ITestDbContext, TestDbContext>();

            // assert
            Assert.NotNull(builder);
            Assert.Same(services, builder.Services);
        }

        [Fact]
        public void When_UnitOfWork_Is_Overridden_With_Mock_Should_Resolve_TransactionFilterAttribute()
        {
            // arrange
            var services = new ServiceCollection();
            services.AddUnitOfWorkForWebApi<ITestDbContext, TestDbContext>();
            // replace the real (DbContext-backed) unit of work with a mock so no database is needed
            services.AddScoped(_ => Mock.Of<IUnitOfWork>());
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // act
            var filter = scope.ServiceProvider.GetService<TransactionFilterAttribute>();

            // assert
            Assert.NotNull(filter);
        }

        [Fact]
        public void When_AddUnitOfWorkTransactionAttribute_Is_Called_Should_Add_Global_Filter_For_TransactionFilterAttribute()
        {
            // arrange
            var filters = new FilterCollection();

            // act
            filters.AddUnitOfWorkTransactionAttribute();

            // assert
            var filter = Assert.Single(filters);
            var typeFilter = Assert.IsType<TypeFilterAttribute>(filter);
            Assert.Equal(typeof(TransactionFilterAttribute), typeFilter.ImplementationType);
        }
    }
}
