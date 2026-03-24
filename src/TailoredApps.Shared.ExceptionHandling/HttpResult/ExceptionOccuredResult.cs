using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.HttpResult
{
    /// <summary>Wynik HTTP 400 zwracany gdy wystąpi wyjątek lub błąd walidacji.</summary>
    public class ExceptionOccuredResult : ObjectResult
    {
        /// <summary>Inicjalizuje wynik 400 z błędami walidacji modelu.</summary>
        public ExceptionOccuredResult(ModelStateDictionary modelState)
            : base(modelState)
        {
            StatusCode = StatusCodes.Status400BadRequest;
        }

        /// <summary>Inicjalizuje wynik 400 z modelem odpowiedzi wyjątku.</summary>
        public ExceptionOccuredResult(ExceptionHandlingResultModel modelState)
            : base(modelState)
        {
            StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
