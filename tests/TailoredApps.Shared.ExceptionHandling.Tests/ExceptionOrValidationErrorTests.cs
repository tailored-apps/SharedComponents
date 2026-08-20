using TailoredApps.Shared.ExceptionHandling.Model;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class ExceptionOrValidationErrorTests
    {
        [Fact]
        public void When_Field_Is_Empty_Should_Set_Field_To_Null()
        {
            // arrange
            // act
            var error = new ExceptionOrValidationError(string.Empty, "Some message");

            // assert
            Assert.Null(error.Field);
            Assert.Equal("Some message", error.Message);
        }

        [Fact]
        public void When_Field_Is_Null_Should_Keep_Field_Null()
        {
            // arrange
            // act
            var error = new ExceptionOrValidationError(null, "Some message");

            // assert
            Assert.Null(error.Field);
        }

        [Fact]
        public void When_Field_Is_Provided_Should_Preserve_Field_And_Message()
        {
            // arrange
            // act
            var error = new ExceptionOrValidationError("Name", "Name is required");

            // assert
            Assert.Equal("Name", error.Field);
            Assert.Equal("Name is required", error.Message);
        }
    }
}
