using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class ExceptionHandlingServiceTests
    {
        [Fact]
        public void When_Constructed_Should_Expose_The_Provider()
        {
            // arrange
            var provider = Mock.Of<IExceptionHandlingProvider>();

            // act
            var service = new ExceptionHandlingService<IExceptionHandlingProvider>(provider);

            // assert
            Assert.Same(provider, service.ExceptionHandlingProvider);
        }

        [Fact]
        public void When_Response_Is_Called_With_Exception_Should_Delegate_To_Provider()
        {
            // arrange
            var exception = new InvalidOperationException("failure");
            var expected = new ExceptionHandlingResultModel("failure", new List<ExceptionOrValidationError>());
            var providerMock = new Mock<IExceptionHandlingProvider>();
            providerMock.Setup(x => x.Response(exception)).Returns(expected);
            var service = new ExceptionHandlingService<IExceptionHandlingProvider>(providerMock.Object);

            // act
            var result = service.Response(exception);

            // assert
            Assert.Same(expected, result);
            providerMock.Verify(x => x.Response(exception), Times.Once);
            providerMock.Verify(x => x.Response(It.IsAny<ModelStateDictionary>()), Times.Never);
        }

        [Fact]
        public void When_Response_Is_Called_With_ModelState_Should_Delegate_To_Provider()
        {
            // arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "Name is required");
            var expected = new ExceptionHandlingResultModel("Validation Failed", new List<ExceptionOrValidationError>());
            var providerMock = new Mock<IExceptionHandlingProvider>();
            providerMock.Setup(x => x.Response(modelState)).Returns(expected);
            var service = new ExceptionHandlingService<IExceptionHandlingProvider>(providerMock.Object);

            // act
            var result = service.Response(modelState);

            // assert
            Assert.Same(expected, result);
            providerMock.Verify(x => x.Response(modelState), Times.Once);
            providerMock.Verify(x => x.Response(It.IsAny<Exception>()), Times.Never);
        }
    }
}
