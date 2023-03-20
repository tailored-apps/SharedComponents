using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    public class ExceptionHandlingService<T> : IExceptionHandlingService<T> where T : IExceptionHandlingProvider
    {
        public ExceptionHandlingService(T exceptionHandlingProvider)
        {
            this.ExceptionHandlingProvider = exceptionHandlingProvider;
        }
        public T ExceptionHandlingProvider { get; private set; }

        public ExceptionHandlingResultModel Response(Exception exception)
        {
            return ExceptionHandlingProvider.Response(exception);
        }

        public ExceptionHandlingResultModel Response(ModelStateDictionary modelState)
        {
            return ExceptionHandlingProvider.Response(modelState);
        }
    }
}
