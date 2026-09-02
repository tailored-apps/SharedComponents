using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.HttpResult
{
    /// <summary>HTTP result returned when an exception or a model validation error occurred.</summary>
    public class ExceptionOccuredResult : ObjectResult
    {
        /// <summary>Initialises a 400 result carrying the model-state validation errors.</summary>
        public ExceptionOccuredResult(ModelStateDictionary modelState)
            : base(modelState)
        {
            StatusCode = StatusCodes.Status400BadRequest;
        }

        /// <summary>
        /// Initialises a result carrying the exception response model. The HTTP status code is taken
        /// from <see cref="ExceptionHandlingResultModel.ErrorCode"/> when it is a valid error code
        /// (400-599); otherwise 400 is used.
        /// </summary>
        public ExceptionOccuredResult(ExceptionHandlingResultModel modelState)
            : base(modelState)
        {
            var code = modelState?.ErrorCode ?? StatusCodes.Status400BadRequest;
            StatusCode = code >= 400 && code <= 599 ? code : StatusCodes.Status400BadRequest;
        }
    }
}
