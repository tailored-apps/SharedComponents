using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;
using TailoredApps.Shared.ExceptionHandling.HttpResult;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class HandleExceptionFilterAttributeTests
    {
        private class DummyController
        {
            [HandleException]
            public void DecoratedAction()
            {
            }

            public void PlainAction()
            {
            }
        }

        [HandleException]
        private class ClassDecoratedController
        {
            public void PlainAction()
            {
            }
        }

        private static ActionContext CreateActionContext(string actionMethodName)
            => CreateActionContext(typeof(DummyController), actionMethodName);

        private static ActionContext CreateActionContext(System.Type controllerType, string actionMethodName)
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = controllerType.GetMethod(actionMethodName),
                ActionName = actionMethodName,
                ControllerName = controllerType.Name
            };
            return new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor, new ModelStateDictionary());
        }

        private static ActionExecutingContext CreateExecutingContext(ActionContext actionContext)
        {
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new DummyController());
        }

        private static ActionExecutedContext CreateExecutedContext(ActionContext actionContext)
        {
            return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new DummyController());
        }

        [Fact]
        public void When_Action_Has_Attribute_And_ModelState_Is_Invalid_OnActionExecuting_Should_Set_ExceptionOccuredResult()
        {
            // arrange
            var expectedModel = new ExceptionHandlingResultModel("Validation Failed", new List<ExceptionOrValidationError>());
            var serviceMock = new Mock<IExceptionHandlingService>();
            serviceMock.Setup(x => x.Response(It.IsAny<ModelStateDictionary>())).Returns(expectedModel);
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(nameof(DummyController.DecoratedAction));
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executingContext = CreateExecutingContext(actionContext);

            // act
            filter.OnActionExecuting(executingContext);

            // assert
            var result = Assert.IsType<ExceptionOccuredResult>(executingContext.Result);
            Assert.Equal(400, result.StatusCode);
            Assert.Same(expectedModel, result.Value);
            serviceMock.Verify(x => x.Response(actionContext.ModelState), Times.Once);
        }

        [Fact]
        public void When_Action_Has_Attribute_And_ModelState_Is_Valid_OnActionExecuting_Should_Not_Set_Result()
        {
            // arrange
            var serviceMock = new Mock<IExceptionHandlingService>();
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(nameof(DummyController.DecoratedAction));
            var executingContext = CreateExecutingContext(actionContext);

            // act
            filter.OnActionExecuting(executingContext);

            // assert
            Assert.Null(executingContext.Result);
            serviceMock.Verify(x => x.Response(It.IsAny<ModelStateDictionary>()), Times.Never);
        }

        [Fact]
        public void When_Action_Has_No_Attribute_And_ModelState_Is_Invalid_OnActionExecuting_Should_Not_Call_Service()
        {
            // arrange
            var serviceMock = new Mock<IExceptionHandlingService>();
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(nameof(DummyController.PlainAction));
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executingContext = CreateExecutingContext(actionContext);

            // act
            filter.OnActionExecuting(executingContext);

            // assert
            Assert.Null(executingContext.Result);
            serviceMock.Verify(x => x.Response(It.IsAny<ModelStateDictionary>()), Times.Never);
        }

        [Fact]
        public void When_Action_Has_Attribute_And_ModelState_Is_Invalid_OnActionExecuted_Should_Replace_Result()
        {
            // arrange
            var expectedModel = new ExceptionHandlingResultModel("Validation Failed", new List<ExceptionOrValidationError>());
            var serviceMock = new Mock<IExceptionHandlingService>();
            serviceMock.Setup(x => x.Response(It.IsAny<ModelStateDictionary>())).Returns(expectedModel);
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(nameof(DummyController.DecoratedAction));
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executedContext = CreateExecutedContext(actionContext);
            executedContext.Result = new OkResult();

            // act
            filter.OnActionExecuted(executedContext);

            // assert
            var result = Assert.IsType<ExceptionOccuredResult>(executedContext.Result);
            Assert.Equal(400, result.StatusCode);
            Assert.Same(expectedModel, result.Value);
            serviceMock.Verify(x => x.Response(actionContext.ModelState), Times.Once);
        }

        [Fact]
        public void When_Action_Has_No_Attribute_And_ModelState_Is_Invalid_OnActionExecuted_Should_Keep_Original_Result()
        {
            // arrange
            var serviceMock = new Mock<IExceptionHandlingService>();
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(nameof(DummyController.PlainAction));
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executedContext = CreateExecutedContext(actionContext);
            var originalResult = new OkResult();
            executedContext.Result = originalResult;

            // act
            filter.OnActionExecuted(executedContext);

            // assert
            Assert.Same(originalResult, executedContext.Result);
            serviceMock.Verify(x => x.Response(It.IsAny<ModelStateDictionary>()), Times.Never);
        }

        [Fact]
        public void When_Controller_Class_Has_Attribute_And_ModelState_Is_Invalid_OnActionExecuting_Should_Set_ExceptionOccuredResult()
        {
            // arrange
            var expectedModel = new ExceptionHandlingResultModel("Validation Failed", new List<ExceptionOrValidationError>());
            var serviceMock = new Mock<IExceptionHandlingService>();
            serviceMock.Setup(x => x.Response(It.IsAny<ModelStateDictionary>())).Returns(expectedModel);
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = CreateActionContext(typeof(ClassDecoratedController), nameof(ClassDecoratedController.PlainAction));
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executingContext = CreateExecutingContext(actionContext);

            // act
            filter.OnActionExecuting(executingContext);

            // assert
            var result = Assert.IsType<ExceptionOccuredResult>(executingContext.Result);
            Assert.Same(expectedModel, result.Value);
            serviceMock.Verify(x => x.Response(actionContext.ModelState), Times.Once);
        }

        [Fact]
        public void When_Descriptor_Is_Not_ControllerActionDescriptor_Should_Do_Nothing()
        {
            // arrange
            var serviceMock = new Mock<IExceptionHandlingService>();
            var filter = new HandleExceptionFilterAttribute(serviceMock.Object);
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
            actionContext.ModelState.AddModelError("Name", "Name is required");
            var executingContext = CreateExecutingContext(actionContext);
            var executedContext = CreateExecutedContext(actionContext);

            // act
            filter.OnActionExecuting(executingContext);
            filter.OnActionExecuted(executedContext);

            // assert
            Assert.Null(executingContext.Result);
            Assert.Null(executedContext.Result);
            serviceMock.Verify(x => x.Response(It.IsAny<ModelStateDictionary>()), Times.Never);
        }
    }
}
