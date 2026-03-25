using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using TailoredApps.Shared.ExceptionHandling.HttpResult;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters
{
    /// <summary>
    /// MVC action filter that intercepts actions decorated with <see cref="HandleExceptionAttribute"/>
    /// and automatically returns a structured error response when the model state is invalid.
    /// </summary>
    public class HandleExceptionFilterAttribute : ActionFilterAttribute
    {
        private readonly IExceptionHandlingService exceptionHandlingService;

        /// <summary>
        /// Initializes a new instance of <see cref="HandleExceptionFilterAttribute"/>.
        /// </summary>
        /// <param name="exceptionHandlingService">
        /// The service used to convert model-state errors into structured responses.
        /// </param>
        public HandleExceptionFilterAttribute(IExceptionHandlingService exceptionHandlingService)
        {
            this.exceptionHandlingService = exceptionHandlingService;
        }

        /// <summary>
        /// Called before the action executes. If the action has <see cref="HandleExceptionAttribute"/>
        /// and the model state is invalid, the action result is short-circuited with an error response.
        /// </summary>
        /// <param name="actionContext">The context for the executing action.</param>
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

        /// <summary>
        /// Called after the action executes. If the action has <see cref="HandleExceptionAttribute"/>
        /// and the model state is invalid, the result is replaced with a structured error response.
        /// </summary>
        /// <param name="actionContext">The context for the executed action.</param>
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
