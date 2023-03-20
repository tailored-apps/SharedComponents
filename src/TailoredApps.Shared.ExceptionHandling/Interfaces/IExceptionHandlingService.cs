using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    public interface IExceptionHandlingService
    {
        ExceptionHandlingResultModel Response(Exception exception);
        ExceptionHandlingResultModel Response(ModelStateDictionary modelState);
    }

    public interface IExceptionHandlingService<T> : IExceptionHandlingService where T : IExceptionHandlingProvider
    {
        public T ExceptionHandlingProvider { get; }
    }
}
