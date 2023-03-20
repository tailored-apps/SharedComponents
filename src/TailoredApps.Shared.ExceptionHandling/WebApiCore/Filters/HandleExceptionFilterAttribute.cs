using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using TailoredApps.Shared.ExceptionHandling.HttpResult;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters
{
    public class HandleExceptionFilterAttribute : ActionFilterAttribute
    {
        private readonly IExceptionHandlingService exceptionHandlingService;
        public HandleExceptionFilterAttribute(IExceptionHandlingService exceptionHandlingService)
        {
            this.exceptionHandlingService = exceptionHandlingService;
        }

        public override void OnActionExecuting(ActionExecutingContext actionContext)
        {
            var controllerActionDescriptor = actionContext.ActionDescriptor as ControllerActionDescriptor;
            var handleExceptionAttribute = controllerActionDescriptor?.MethodInfo
                .GetCustomAttribute<HandleExceptionAttribute>(true);

            if (handleExceptionAttribute != null)
            {
                if (!actionContext.ModelState.IsValid)
                {
                    actionContext.Result = new ExceptionOccuredResult(exceptionHandlingService.Response(actionContext.ModelState));
                }
            }
            base.OnActionExecuting(actionContext);
        }

        public override void OnActionExecuted(ActionExecutedContext actionContext)
        {
            var controllerActionDescriptor = actionContext.ActionDescriptor as ControllerActionDescriptor;
            var handleExceptionAttribute = controllerActionDescriptor?.MethodInfo
                .GetCustomAttribute<HandleExceptionAttribute>(true);

            if (handleExceptionAttribute != null)
            {
                if (!actionContext.ModelState.IsValid)
                {
                    actionContext.Result = new ExceptionOccuredResult(exceptionHandlingService.Response(actionContext.ModelState));
                }
            }
            base.OnActionExecuted(actionContext);
        }
    }
}
