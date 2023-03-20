using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    public interface IExceptionHandlingProvider
    {
        ExceptionHandlingResultModel Response(Exception exception);
        ExceptionHandlingResultModel Response(ModelStateDictionary modelState);
    }
}
