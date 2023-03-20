using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Utils;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit
{
    public class UnitOfWorkOptionsBuilderTests
    {
        [Fact]
        public void Should_Throw_When_No_Action_Is_Specified()
        {
            // arrange
            var services = new ServiceCollection();
            var optionsBuilder = services.AddUnitOfWork<IExampleDbContext, InMemoryDbContext>();

            // act & assert
            Assert.Throws<ArgumentNullException>(() => optionsBuilder.WithUnitOfWorkAudit<DbContext, AuditorForTests>(null));
        }

        [Fact]
        public void Should_Register_Required_Types()
        {
            // arrange
            var services = new ServiceCollection();
            services.AddDbContext<InMemoryDbContext>();
            var optionsBuilder = services.AddUnitOfWork<IExampleDbContext, InMemoryDbContext>();

            // act
            optionsBuilder.WithUnitOfWorkAudit<InMemoryDbContext, AuditorForTests>(SettingsAction);

            // assert
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<IAuditSettings>();
            serviceProvider.GetRequiredService<IEntityChangesAuditor>();
            serviceProvider.GetRequiredService<IUnitOfWorkAuditContext>();
            serviceProvider.GetRequiredService<IAuditChangesCollector>();
            Assert.Contains(serviceProvider.GetServices<IHook>(), hook => hook is IPostSaveChangesHook);
            Assert.Contains(serviceProvider.GetServices<IHook>(), hook => hook is IPreSaveChangesHook);
            Assert.Contains(serviceProvider.GetServices<IHook>(), hook => hook is ITransactionRollbackHook);
            Assert.Contains(serviceProvider.GetServices<IHook>(), hook => hook is ITransactionCommitHook);
            Assert.True(IsImplementedWithLifetime(services.Single(serviceDescriptor => serviceDescriptor.ServiceType == typeof(IUnitOfWorkAuditContext)), ServiceLifetime.Scoped));

            void SettingsAction(IAuditSettings settings)
            {
                settings.TypesToCollect = new List<Type> { typeof(ExampleEntity) };
                settings.EntityStatesToCollect = new List<AuditEntityState> { AuditEntityState.Added, AuditEntityState.Modified };
            }
        }

        private bool IsImplementedWithLifetime(ServiceDescriptor serviceDescriptor, ServiceLifetime serviceLifetime)
        {
            return serviceDescriptor.Lifetime == serviceLifetime;
        }
    }
}
