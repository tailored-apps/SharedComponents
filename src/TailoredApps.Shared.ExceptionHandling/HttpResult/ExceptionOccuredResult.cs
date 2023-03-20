using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.HttpResult
{
    public class ExceptionOccuredResult : ObjectResult
    {
        public ExceptionOccuredResult(ModelStateDictionary modelState)
            : base(modelState)
        {
            StatusCode = StatusCodes.Status400BadRequest;
        }



        public ExceptionOccuredResult(ExceptionHandlingResultModel modelState)
            : base(modelState)
        {
            StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
