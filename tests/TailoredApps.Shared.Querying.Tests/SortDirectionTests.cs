using System;
using Xunit;

namespace TailoredApps.Shared.Querying.Tests
{
    public class SortDirectionTests
    {
        [Theory]
        [InlineData(SortDirection.Undefined, 0)]
        [InlineData(SortDirection.Asc, 1)]
        [InlineData(SortDirection.Desc, 2)]
        public void Should_Have_Stable_Underlying_Values(SortDirection direction, int expectedValue)
        {
            // arrange
            // act
            var actualValue = (int)direction;

            // assert
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void Should_Define_Exactly_Undefined_Asc_And_Desc_Members()
        {
            // arrange
            var expected = new[] { SortDirection.Undefined, SortDirection.Asc, SortDirection.Desc };

            // act
            var actual = (SortDirection[])Enum.GetValues(typeof(SortDirection));

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void When_Default_Value_Used_Should_Be_Undefined()
        {
            // arrange
            // act
            var defaultValue = default(SortDirection);

            // assert
            Assert.Equal(SortDirection.Undefined, defaultValue);
        }
    }
}
