using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests.TestSupport
{
    /// <summary>
    /// Dummy controller whose methods act as targets for the
    /// <see cref="ControllerActionDescriptor.MethodInfo"/> used in filter tests.
    /// </summary>
    internal class DummyController
    {
        public IActionResult PlainAction() => new OkResult();

        [TransactionIsolationLevel(IsolationLevel.Serializable)]
        public IActionResult SerializableAction() => new OkResult();
    }

    /// <summary>
    /// Builds <see cref="ActionExecutingContext"/> / <see cref="ActionExecutedContext"/>
    /// instances by hand (no MVC pipeline) for unit testing action filters.
    /// </summary>
    internal static class FilterContextFactory
    {
        internal static ActionContext CreateActionContext(string actionMethodName)
        {
            var descriptor = new ControllerActionDescriptor
            {
                MethodInfo = typeof(DummyController).GetMethod(actionMethodName),
                ControllerTypeInfo = typeof(DummyController).GetTypeInfo(),
                ControllerName = nameof(DummyController),
                ActionName = actionMethodName
            };

            return new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
        }

        internal static ActionContext CreateNonControllerActionContext()
            => new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());

        internal static ActionExecutingContext CreateExecutingContext(string actionMethodName = nameof(DummyController.PlainAction))
            => CreateExecutingContext(CreateActionContext(actionMethodName));

        internal static ActionExecutingContext CreateExecutingContext(ActionContext actionContext)
            => new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new DummyController());

        internal static ActionExecutedContext CreateExecutedContext(string actionMethodName = nameof(DummyController.PlainAction))
            => new ActionExecutedContext(
                CreateActionContext(actionMethodName),
                new List<IFilterMetadata>(),
                new DummyController());
    }
}
