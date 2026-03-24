using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    /// <summary>Interfejs dostawcy obsługi wyjątków — mapuje wyjątki na modele odpowiedzi HTTP.</summary>
    public interface IExceptionHandlingProvider
    {
        /// <summary>Tworzy model odpowiedzi dla danego wyjątku.</summary>
        ExceptionHandlingResultModel Response(Exception exception);

        /// <summary>Tworzy model odpowiedzi dla błędów walidacji modelu.</summary>
        ExceptionHandlingResultModel Response(ModelStateDictionary modelState);
    }
}
