using System;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit
{
    public class EntityChangeTests
    {
        [Fact]
        public void Should_Throw_When_Current_Entity_Is_Null()
        {
            // act & assert
            Assert.Throws<ArgumentNullException>(() => new EntityChange<ExampleEntity>(null, new ExampleEntity(), null, AuditEntityState.Modified));
        }

        [Fact]
        public void Should_Throw_When_Original_Entity_Is_Null()
        {
            // act & assert
            Assert.Throws<ArgumentNullException>(() => new EntityChange<ExampleEntity>(new ExampleEntity(), null, null, AuditEntityState.Modified));
        }

        [Fact]
        public void Should_Create_EntityChange_Of_ExampleEntity()
        {
            // act
            var entityChange = new EntityChange<ExampleEntity>(new ExampleEntity() { Prop = "currentProp" }, new ExampleEntity() { Prop = "originalProp" }, null, AuditEntityState.Modified);

            // assert
            Assert.Equal(AuditEntityState.Modified, entityChange.State);
            Assert.Equal("currentProp", entityChange.CurrentEntity.Prop);
            Assert.Equal("originalProp", entityChange.OriginalEntity.Prop);
        }
    }
}
