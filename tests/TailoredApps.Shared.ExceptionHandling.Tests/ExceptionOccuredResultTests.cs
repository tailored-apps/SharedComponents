using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.HttpResult;
using TailoredApps.Shared.ExceptionHandling.Model;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class ExceptionOccuredResultTests
    {
        [Fact]
        public void When_Created_From_ModelState_Should_Be_ObjectResult_With_Status_400()
        {
            // arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "Name is required");

            // act
            var result = new ExceptionOccuredResult(modelState);

            // assert
            Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(400, result.StatusCode);
            Assert.Same(modelState, result.Value);
        }

        [Fact]
        public void When_Created_From_ResultModel_Should_Be_ObjectResult_With_Status_400()
        {
            // arrange
            var model = new ExceptionHandlingResultModel("failure", new List<ExceptionOrValidationError>());

            // act
            var result = new ExceptionOccuredResult(model);

            // assert
            Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(400, result.StatusCode);
            Assert.Same(model, result.Value);
        }

        [Theory]
        [InlineData(404, 404)]
        [InlineData(422, 422)]
        [InlineData(500, 500)]
        [InlineData(200, 400)]
        [InlineData(0, 400)]
        public void When_Created_From_ResultModel_With_Explicit_Code_Should_Use_Valid_Error_Codes(int code, int expected)
        {
            // arrange
            var model = new ExceptionHandlingResultModel(code, "failure", new List<ExceptionOrValidationError>());

            // act
            var result = new ExceptionOccuredResult(model);

            // assert
            Assert.Equal(expected, result.StatusCode);
        }
    }
}
