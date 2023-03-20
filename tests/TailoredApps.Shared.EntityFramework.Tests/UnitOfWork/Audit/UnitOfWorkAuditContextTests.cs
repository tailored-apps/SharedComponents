
using AutoMoqCore;
using Moq;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit
{
    public class UnitOfWorkAuditContextTests
    {
        private AutoMoqer _autoMoqer;
        private Mock<IAuditChangesCollector> _collectorMock;
        private IUnitOfWorkAuditContext _sut;

        public UnitOfWorkAuditContextTests()
        {
            _autoMoqer = new AutoMoqer();
            _sut = CreateUnitOfWorkAuditContext();
        }

        [Fact]
        public void Should_Clear_Changes_On_Discard()
        {
            // arrange
            _collectorMock.Setup(collector => collector.CollectChanges())
                .Returns(GetSampleChangesList());
            _sut.CollectChanges();
            _sut.PostCollectChanges();

            // act
            _sut.DiscardChanges();

            // assert
            _sut.AuditChanges();
            _autoMoqer.GetMock<IEntityChangesAuditor>().Verify(auditor => auditor.AuditChanges(It.IsAny<IEnumerable<EntityChange>>()), Times.Never);
        }

        [Fact]
        public void Should_Call_Auditor()
        {
            // arrange
            _collectorMock.Setup(collector => collector.CollectChanges())
                .Returns(GetSampleChangesList());
            _sut.CollectChanges();
            _sut.PostCollectChanges();

            // act
            _sut.AuditChanges();

            // assert
            _autoMoqer.GetMock<IEntityChangesAuditor>().Verify(auditor => auditor.AuditChanges(It.IsAny<IEnumerable<EntityChange>>()), Times.Once);
        }

        private IUnitOfWorkAuditContext CreateUnitOfWorkAuditContext()
        {
            var sut = _autoMoqer.Create<UnitOfWorkAuditContext>();
            _collectorMock = _autoMoqer.GetMock<IAuditChangesCollector>();
            return sut;
        }

        private static IEnumerable<IAuditEntityEntry> GetSampleChangesList()
        {
            return new List<IAuditEntityEntry>
            {
                TestingHelpers.CreateAuditEntityEntry(new ExampleEntity(), new ExampleEntity(), "ExampleEntity_1", AuditEntityState.Modified),
                TestingHelpers.CreateAuditEntityEntry(new ExampleEntity(), new ExampleEntity(), "ExampleEntity_2", AuditEntityState.Modified)
            };
        }
    }
}
