using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Model;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class ExceptionHandlingResultModelTests
    {
        [Fact]
        public void When_Created_From_ModelState_Should_Flatten_Multiple_Errors_Per_Key()
        {
            // arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "Name is required");
            modelState.AddModelError("Name", "Name is too short");
            modelState.AddModelError("Email", "Email is invalid");

            // act
            var model = new ExceptionHandlingResultModel(modelState);

            // assert
            Assert.Equal(3, model.Errors.Count);
            Assert.Contains(model.Errors, x => x.Field == "Name" && x.Message == "Name is required");
            Assert.Contains(model.Errors, x => x.Field == "Name" && x.Message == "Name is too short");
            Assert.Contains(model.Errors, x => x.Field == "Email" && x.Message == "Email is invalid");
        }

        [Fact]
        public void When_Created_From_ModelState_Should_Set_ErrorCode_400_And_Validation_Failed_Message()
        {
            // arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "Name is required");

            // act
            var model = new ExceptionHandlingResultModel(modelState);

            // assert
            Assert.Equal(400, model.ErrorCode);
            Assert.Equal("Validation Failed", model.Message);
        }

        [Fact]
        public void When_Created_With_Message_And_Errors_Should_Default_ErrorCode_To_400()
        {
            // arrange
            var errors = new List<ExceptionOrValidationError>
            {
                new ExceptionOrValidationError("Name", "Name is required")
            };

            // act
            var model = new ExceptionHandlingResultModel("Some failure", errors);

            // assert
            Assert.Equal(400, model.ErrorCode);
            Assert.Equal("Some failure", model.Message);
            Assert.Single(model.Errors);
        }

        [Fact]
        public void When_Created_With_Explicit_ErrorCode_Should_Use_That_Code()
        {
            // arrange
            var errors = new List<ExceptionOrValidationError>();

            // act
            var model = new ExceptionHandlingResultModel(500, "Server failure", errors);

            // assert
            Assert.Equal(500, model.ErrorCode);
            Assert.Equal("Server failure", model.Message);
            Assert.Empty(model.Errors);
        }

        [Fact]
        public void When_ToString_Is_Called_Should_Produce_CamelCase_Json()
        {
            // arrange
            var errors = new List<ExceptionOrValidationError>
            {
                new ExceptionOrValidationError("Name", "Name is required")
            };
            var model = new ExceptionHandlingResultModel("Validation Failed", errors);

            // act
            var json = model.ToString();

            // assert
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.Equal("Validation Failed", root.GetProperty("message").GetString());
            Assert.Equal(400, root.GetProperty("errorCode").GetInt32());
            var jsonErrors = root.GetProperty("errors");
            Assert.Equal(1, jsonErrors.GetArrayLength());
            Assert.Equal("Name", jsonErrors[0].GetProperty("field").GetString());
            Assert.Equal("Name is required", jsonErrors[0].GetProperty("message").GetString());
        }

        [Fact]
        public void When_ToString_Is_Called_And_Field_Is_Null_Should_Omit_Field_From_Json()
        {
            // arrange
            var errors = new List<ExceptionOrValidationError>
            {
                new ExceptionOrValidationError(string.Empty, "General failure")
            };
            var model = new ExceptionHandlingResultModel("Failure", errors);

            // act
            var json = model.ToString();

            // assert
            using var document = JsonDocument.Parse(json);
            var jsonError = document.RootElement.GetProperty("errors")[0];
            Assert.False(jsonError.TryGetProperty("field", out _));
            Assert.Equal("General failure", jsonError.GetProperty("message").GetString());
        }
    }
}
