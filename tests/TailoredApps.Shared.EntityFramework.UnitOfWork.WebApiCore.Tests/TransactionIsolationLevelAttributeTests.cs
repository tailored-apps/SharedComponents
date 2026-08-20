using System;
using System.Data;
using System.Reflection;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests.TestSupport;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests
{
    public class TransactionIsolationLevelAttributeTests
    {
        [Theory]
        [InlineData(IsolationLevel.ReadUncommitted)]
        [InlineData(IsolationLevel.ReadCommitted)]
        [InlineData(IsolationLevel.RepeatableRead)]
        [InlineData(IsolationLevel.Serializable)]
        [InlineData(IsolationLevel.Snapshot)]
        public void When_Constructed_With_Level_Should_Expose_It_Via_Level_Property(IsolationLevel level)
        {
            // arrange & act
            var attribute = new TransactionIsolationLevelAttribute(level);

            // assert
            Assert.Equal(level, attribute.Level);
        }

        [Fact]
        public void Should_Be_Usable_On_Classes_And_Methods_Only_Once_And_Be_Inherited()
        {
            // arrange
            var usage = typeof(TransactionIsolationLevelAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            // act & assert
            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, usage.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.True(usage.Inherited);
        }

        [Fact]
        public void When_Applied_To_Action_Method_Should_Be_Discoverable_Via_Reflection()
        {
            // arrange
            var methodInfo = typeof(DummyController).GetMethod(nameof(DummyController.SerializableAction));

            // act
            var attribute = methodInfo.GetCustomAttribute<TransactionIsolationLevelAttribute>(true);

            // assert
            Assert.NotNull(attribute);
            Assert.Equal(IsolationLevel.Serializable, attribute.Level);
        }
    }
}
