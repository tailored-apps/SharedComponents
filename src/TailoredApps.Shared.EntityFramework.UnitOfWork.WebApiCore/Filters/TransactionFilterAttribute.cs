using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters
{
    /// <summary>
    /// ASP.NET Core action filter that automatically wraps each controller action
    /// in a Unit of Work transaction.
    /// Commits when the action succeeds; rolls back when the action threw or returned an error
    /// status code (4xx/5xx), so partial work behind a <c>BadRequest</c>/<c>Conflict</c> result is
    /// never persisted.
    /// The isolation level can be overridden per-action via <see cref="TransactionIsolationLevelAttribute"/>.
    /// </summary>
    public class TransactionFilterAttribute : ActionFilterAttribute
    {
        private readonly IUnitOfWork _uow;

        /// <summary>
        /// Initialises the filter with the Unit of Work instance resolved from the DI container.
        /// </summary>
        /// <param name="uow">The scoped Unit of Work for the current HTTP request.</param>
        public TransactionFilterAttribute(IUnitOfWork uow)
        {
            _uow = uow;
        }

        /// <summary>
        /// Called before the action executes.
        /// Applies a custom isolation level when the action is decorated with <see cref="TransactionIsolationLevelAttribute"/>.
        /// </summary>
        /// <param name="actionContext">The executing action context.</param>
        public override void OnActionExecuting(ActionExecutingContext actionContext)
        {
            //check if the action has explicitly stated which isolation level should be set in unit of work
            var controllerActionDescriptor = actionContext.ActionDescriptor as ControllerActionDescriptor;
            var isolationLevelAttribute = controllerActionDescriptor?.MethodInfo
                .GetCustomAttribute<TransactionIsolationLevelAttribute>(true);

            if (isolationLevelAttribute != null)
            {
                // We need a container per request, therefore we cannot inject dependencies with StructureMap,
                // because we would obtain them from root container, not nested container (there is no way to get
                // nested container when creating a new TransactionFilter instance or via FilterProvider).
                _uow.SetIsolationLevel(isolationLevelAttribute.Level);
            }

            base.OnActionExecuting(actionContext);
        }

        /// <summary>
        /// Called after the action executes.
        /// Commits the transaction on success or rolls it back when an exception occurred.
        /// </summary>
        /// <param name="actionExecutedContext">The executed action context.</param>
        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            // We need a container per request, therefore we cannot inject dependencies with StructureMap,
            // because we would obtain them from root container, not nested container (there is no way to get
            // nested container when creating a new TransactionFilter instance or via FilterProvider).

            if (actionExecutedContext.Exception != null || IsErrorResult(actionExecutedContext.Result))
            {
                _uow.RollbackTransaction();
            }
            else
            {
                try
                {
                    _uow.CommitTransaction();
                }
                catch (Exception commitException)
                {
                    Exception failure = commitException;
                    try
                    {
                        _uow.RollbackTransaction();
                    }
                    catch (Exception rollbackException)
                    {
                        // Keep the original cause visible instead of replacing it with the rollback error.
                        failure = new AggregateException(commitException, rollbackException);
                    }

                    actionExecutedContext.Exception = failure;
                    actionExecutedContext.Result = null;
                }
            }

            base.OnActionExecuted(actionExecutedContext);
        }

        /// <summary>
        /// Returns <c>true</c> when the action produced a result with a 4xx or 5xx status code.
        /// </summary>
        internal static bool IsErrorResult(Microsoft.AspNetCore.Mvc.IActionResult result)
            => result is IStatusCodeActionResult { StatusCode: >= 400 };
    }
}
