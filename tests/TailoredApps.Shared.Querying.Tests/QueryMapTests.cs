using System;
using System.Linq.Expressions;
using Xunit;

namespace TailoredApps.Shared.Querying.Tests
{
    public class QueryMapTests
    {
        private sealed class DestinationModel
        {
            public string DisplayName { get; set; }
        }

        private sealed class SourceEntity
        {
            public string Name { get; set; }
        }

        [Fact]
        public void When_Constructed_Should_Store_Destination_From_First_Parameter_And_Source_From_Second()
        {
            // arrange
            // The ctor takes (destination, source) — this pins the parameter order so a silent swap fails the test.
            Expression<Func<DestinationModel, object>> destination = d => d.DisplayName;
            Expression<Func<SourceEntity, object>> source = s => s.Name;

            // act
            var map = new QueryMap<DestinationModel, SourceEntity>(destination, source);

            // assert
            Assert.Same(destination, map.Destination);
            Assert.Same(source, map.Source);
        }

        [Fact]
        public void When_Constructed_Should_Expose_Compilable_Expressions_Selecting_The_Mapped_Fields()
        {
            // arrange
            Expression<Func<DestinationModel, object>> destination = d => d.DisplayName;
            Expression<Func<SourceEntity, object>> source = s => s.Name;
            var map = new QueryMap<DestinationModel, SourceEntity>(destination, source);
            var destinationInstance = new DestinationModel { DisplayName = "dto-value" };
            var sourceInstance = new SourceEntity { Name = "entity-value" };

            // act
            var destinationValue = map.Destination.Compile()(destinationInstance);
            var sourceValue = map.Source.Compile()(sourceInstance);

            // assert
            Assert.Equal("dto-value", destinationValue);
            Assert.Equal("entity-value", sourceValue);
        }

        [Fact]
        public void When_Constructed_With_Null_Expressions_Should_Store_Nulls()
        {
            // arrange
            // Pins current behavior: the ctor does not validate its arguments,
            // so null expressions are accepted and stored as-is.

            // act
            var map = new QueryMap<DestinationModel, SourceEntity>(null, null);

            // assert
            Assert.Null(map.Destination);
            Assert.Null(map.Source);
        }
    }
}
