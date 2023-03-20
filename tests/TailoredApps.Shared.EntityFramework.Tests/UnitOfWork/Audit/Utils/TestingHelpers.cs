using Moq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils
{
    internal static class TestingHelpers
    {
        public static IAuditEntityEntry CreateAuditEntityEntry(ExampleEntity originalEntity, ExampleEntity currentEntity, string primaryKeyIdentifier, AuditEntityState entityState)
        {
            var auditEntityEntryMock = new Mock<IAuditEntityEntry>();
            auditEntityEntryMock.Setup(e => e.EntityState).Returns(entityState);
            auditEntityEntryMock.Setup(e => e.OriginalEntity).Returns(originalEntity);
            auditEntityEntryMock.Setup(e => e.CurrentEntity).Returns(currentEntity);
            auditEntityEntryMock.Setup(e => e.EntityType).Returns(typeof(ExampleEntity));
            auditEntityEntryMock.Setup(e => e.GetPrimaryKeyStringIdentifier()).Returns(primaryKeyIdentifier);
            return auditEntityEntryMock.Object;
        }
    }
}
