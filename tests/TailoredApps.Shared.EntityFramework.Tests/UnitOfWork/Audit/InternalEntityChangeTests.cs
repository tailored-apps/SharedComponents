using System;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit
{

    public class InternalEntityChangeTests
    {
        [Fact]
        public void Should_Create_InternalEntityChange_Of_ExampleEntity()
        {
            // arrange
            var auditEntityEntry = TestingHelpers.CreateAuditEntityEntry(new ExampleEntity(), new ExampleEntity(), "ExampleEntity_1", AuditEntityState.Modified);

            // act 
            var internalEntityChange = auditEntityEntry.CreateInternalEntityChange();

            // assert
            Assert.NotNull(internalEntityChange as EntityChange);
            Assert.NotNull(internalEntityChange as EntityChange<ExampleEntity>);
        }

        [Fact]
        public void Should_Throw_When_Type_Mismatch_Occurs_On_Setting_Entity()
        {
            // arrange
            var auditEntityEntry = TestingHelpers.CreateAuditEntityEntry(new ExampleEntity(), new ExampleEntity(), "ExampleEntity_1", AuditEntityState.Modified);
            var internalEntityChange = auditEntityEntry.CreateInternalEntityChange();

            // act & assert
            Assert.Throws<ArgumentException>(() => internalEntityChange.SetCurrentEntity(new SecondExampleEntity()));
            Assert.Throws<ArgumentException>(() => internalEntityChange.SetOriginalEntity(new SecondExampleEntity()));
        }

        [Fact]
        public void Should_Set_Current_Entity()
        {
            // arrange
            var auditEntityEntry = TestingHelpers.CreateAuditEntityEntry(new ExampleEntity(), new ExampleEntity() { Prop = "example" }, "ExampleEntity_1", AuditEntityState.Modified);
            var internalEntityChange = auditEntityEntry.CreateInternalEntityChange();

            // act 
            internalEntityChange.SetCurrentEntity(new ExampleEntity() { Prop = "new example" });

            // assert
            Assert.Equal("new example", (internalEntityChange.GetCurrentEntity() as ExampleEntity)?.Prop);
        }

        [Fact]
        public void Should_Set_Original_Entity()
        {
            // arrange
            var auditEntityEntry = TestingHelpers.CreateAuditEntityEntry(new ExampleEntity() { Prop = "example" }, new ExampleEntity(), "ExampleEntity_1", AuditEntityState.Modified);
            var internalEntityChange = auditEntityEntry.CreateInternalEntityChange();

            // act 
            internalEntityChange.SetOriginalEntity(new ExampleEntity() { Prop = "new example" });

            // assert
            Assert.Equal("new example", (internalEntityChange.GetOriginalEntity() as ExampleEntity)?.Prop);
        }
    }
}
