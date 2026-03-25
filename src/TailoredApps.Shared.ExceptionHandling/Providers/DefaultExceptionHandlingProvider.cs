using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Providers
{
    /// <summary>
    /// Default implementation of <see cref="IExceptionHandlingProvider"/> that handles
    /// FluentValidation <see cref="ValidationException"/> instances and general exceptions,
    /// converting them into <see cref="ExceptionHandlingResultModel"/> responses.
    /// </summary>
    public class DefaultExceptionHandlingProvider : IExceptionHandlingProvider
    {
        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from the given exception.
        /// If the root cause is a FluentValidation <see cref="ValidationException"/>,
        /// individual validation errors are mapped; otherwise the exception message is used.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <returns>A result model describing the error.</returns>
        public ExceptionHandlingResultModel Response(Exception exception)
        {
            var sourceException = exception.GetBaseException();
            var validationException = sourceException as ValidationException;
            if (validationException != null)
            {
                var validationData = validationException.Errors.DistinctBy(x => new { x.PropertyName, x.ErrorMessage }).Select(x => new ExceptionOrValidationError(x.PropertyName, x.ErrorMessage));
                return new ExceptionHandlingResultModel(validationException.Message, validationData);
            }
            else
            {
                return new ExceptionHandlingResultModel(sourceException.Message, new List<ExceptionOrValidationError>(new[] {
                new ExceptionOrValidationError("",exception.Message)
            }));
            }
        }

        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from an invalid model state.
        /// </summary>
        /// <param name="modelState">The model state dictionary containing validation errors.</param>
        /// <returns>A result model describing the validation errors.</returns>
        public ExceptionHandlingResultModel Response(ModelStateDictionary modelState)
        {
            return new ExceptionHandlingResultModel(modelState);
        }
    }

    internal static class LinqExceptionHelper
    {
        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var known = new HashSet<TKey>();
            return source.Where(element => known.Add(keySelector(element)));
        }
    }
}
