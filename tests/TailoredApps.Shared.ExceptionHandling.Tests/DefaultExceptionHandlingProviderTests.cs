using System;
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Providers;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class DefaultExceptionHandlingProviderTests
    {
        private readonly DefaultExceptionHandlingProvider provider = new DefaultExceptionHandlingProvider();

        [Fact]
        public void When_ValidationException_Contains_Duplicate_Errors_Should_Deduplicate_By_PropertyName_And_ErrorMessage()
        {
            // arrange
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Name is required"),
                new ValidationFailure("Name", "Name is required"),
                new ValidationFailure("Name", "Name is too short"),
                new ValidationFailure("Email", "Email is required")
            };
            var exception = new ValidationException("Validation failed", failures);

            // act
            var result = provider.Response(exception);

            // assert
            Assert.Equal(3, result.Errors.Count);
            Assert.Equal("Name", result.Errors[0].Field);
            Assert.Equal("Name is required", result.Errors[0].Message);
            Assert.Equal("Name", result.Errors[1].Field);
            Assert.Equal("Name is too short", result.Errors[1].Message);
            Assert.Equal("Email", result.Errors[2].Field);
            Assert.Equal("Email is required", result.Errors[2].Message);
        }

        [Fact]
        public void When_ValidationException_Contains_Distinct_Errors_Should_Preserve_All_Of_Them()
        {
            // arrange
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Value is required"),
                new ValidationFailure("Email", "Value is required"),
                new ValidationFailure("Name", "Value is too short")
            };
            var exception = new ValidationException("Validation failed", failures);

            // act
            var result = provider.Response(exception);

            // assert
            Assert.Equal(3, result.Errors.Count);
        }

        [Fact]
        public void When_ValidationException_Is_Handled_Should_Use_Its_Message_And_ErrorCode_400()
        {
            // arrange
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Name is required")
            };
            var exception = new ValidationException("Custom validation message", failures);

            // act
            var result = provider.Response(exception);

            // assert
            Assert.Equal("Custom validation message", result.Message);
            Assert.Equal(400, result.ErrorCode);
        }

        [Fact]
        public void When_ValidationException_Is_Wrapped_In_Another_Exception_Should_Be_Found_Via_BaseException()
        {
            // arrange
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Name is required")
            };
            var validationException = new ValidationException("Inner validation failed", failures);
            var wrapped = new InvalidOperationException("Outer wrapper", validationException);

            // act
            var result = provider.Response(wrapped);

            // assert
            Assert.Equal("Inner validation failed", result.Message);
            Assert.Equal(400, result.ErrorCode);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Name", error.Field);
            Assert.Equal("Name is required", error.Message);
        }

        [Fact]
        public void When_Exception_Is_Not_ValidationException_Should_Return_Single_Error_With_Null_Field()
        {
            // arrange
            var exception = new InvalidOperationException("Something went wrong");

            // act
            var result = provider.Response(exception);

            // assert
            Assert.Equal("Something went wrong", result.Message);
            Assert.Equal(400, result.ErrorCode);
            var error = Assert.Single(result.Errors);
            Assert.Null(error.Field);
            Assert.Equal("Something went wrong", error.Message);
        }

        [Fact]
        public void When_NonValidation_Exception_Is_Wrapped_Should_Use_Base_Exception_Message_Consistently()
        {
            // arrange
            var inner = new ArgumentException("inner failure");
            var outer = new Exception("outer failure", inner);

            // act
            var result = provider.Response(outer);

            // assert - both the model message and the error entry report the root cause
            Assert.Equal("inner failure", result.Message);
            var error = Assert.Single(result.Errors);
            Assert.Null(error.Field);
            Assert.Equal("inner failure", error.Message);
        }

        [Fact]
        public void When_ModelState_Is_Handled_Should_Return_Validation_Failed_Model()
        {
            // arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "Name is required");

            // act
            var result = provider.Response(modelState);

            // assert
            Assert.Equal(400, result.ErrorCode);
            Assert.Equal("Validation Failed", result.Message);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Name", error.Field);
            Assert.Equal("Name is required", error.Message);
        }
    }
}
