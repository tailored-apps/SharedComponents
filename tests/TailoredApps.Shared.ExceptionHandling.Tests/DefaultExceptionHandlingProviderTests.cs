using System;
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using Moq;
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
        public void When_Exception_Is_Not_ValidationException_Should_Return_500_With_Generic_Message()
        {
            // arrange
            var exception = new InvalidOperationException("Login failed for user 'app_user' on host db01.internal");

            // act
            var result = provider.Response(exception);

            // assert - the raw exception message must never reach the client by default
            Assert.Equal(500, result.ErrorCode);
            Assert.Equal(DefaultExceptionHandlingProvider.GenericErrorMessage, result.Message);
            var error = Assert.Single(result.Errors);
            Assert.Null(error.Field);
            Assert.Equal(DefaultExceptionHandlingProvider.GenericErrorMessage, error.Message);
            Assert.DoesNotContain("db01.internal", result.ToString());
        }

        [Fact]
        public void When_NonValidation_Exception_Is_Wrapped_Should_Still_Hide_Root_Cause_Message()
        {
            // arrange
            var inner = new ArgumentException("inner failure");
            var outer = new Exception("outer failure", inner);

            // act
            var result = provider.Response(outer);

            // assert
            Assert.Equal(500, result.ErrorCode);
            Assert.Equal(DefaultExceptionHandlingProvider.GenericErrorMessage, result.Message);
            Assert.DoesNotContain("inner failure", result.ToString());
            Assert.DoesNotContain("outer failure", result.ToString());
        }

        [Fact]
        public void When_Details_Are_Enabled_Should_Return_Root_Cause_Message_With_500()
        {
            // arrange
            var detailed = new DefaultExceptionHandlingProvider(includeExceptionDetails: true);
            var outer = new Exception("outer failure", new ArgumentException("inner failure"));

            // act
            var result = detailed.Response(outer);

            // assert
            Assert.Equal(500, result.ErrorCode);
            Assert.Equal("inner failure", result.Message);
            var error = Assert.Single(result.Errors);
            Assert.Equal("inner failure", error.Message);
        }

        [Fact]
        public void When_Host_Is_Development_Should_Include_Details()
        {
            // arrange
            var env = new Mock<IHostEnvironment>();
            env.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
            var dev = new DefaultExceptionHandlingProvider(env.Object);

            // act
            var result = dev.Response(new InvalidOperationException("boom"));

            // assert
            Assert.Equal("boom", result.Message);
        }

        [Fact]
        public void When_Host_Is_Production_Should_Hide_Details()
        {
            // arrange
            var env = new Mock<IHostEnvironment>();
            env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
            var prod = new DefaultExceptionHandlingProvider(env.Object);

            // act
            var result = prod.Response(new InvalidOperationException("boom"));

            // assert
            Assert.Equal(DefaultExceptionHandlingProvider.GenericErrorMessage, result.Message);
        }

        [Fact]
        public void When_Exception_Is_Null_Should_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => provider.Response((Exception)null));
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
