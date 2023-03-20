using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Reflection;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters
{
    public class TransactionFilterAttribute : ActionFilterAttribute
    {
        private readonly IUnitOfWork _uow;

        public TransactionFilterAttribute(IUnitOfWork uow)
        {
            _uow = uow;
        }

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

        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            // We need a container per request, therefore we cannot inject dependencies with StructureMap,
            // because we would obtain them from root container, not nested container (there is no way to get
            // nested container when creating a new TransactionFilter instance or via FilterProvider).

            if (actionExecutedContext.Exception != null) _uow.RollbackTransaction();
            else
            {
                try
                {
                    _uow.CommitTransaction();
                }
                catch (Exception ex)
                {
                    _uow.RollbackTransaction();
                    actionExecutedContext.Exception = ex;
                    actionExecutedContext.Result = null;
                }
            }

            base.OnActionExecuted(actionExecutedContext);
        }
    }
}