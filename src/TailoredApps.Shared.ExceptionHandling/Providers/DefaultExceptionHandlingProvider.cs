using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Providers
{
    public class DefaultExceptionHandlingProvider : IExceptionHandlingProvider
    {
        public ExceptionHandlingResultModel Response(Exception exception)
        {
            var sourceException = exception.GetBaseException();
            var validationException = sourceException as ValidationException;
            if (validationException != null)
            {
                var validationData = validationException.Errors.DistinctBy(x=> new { x.PropertyName, x.ErrorMessage}).Select(x => new ExceptionOrValidationError(x.PropertyName,  x.ErrorMessage ));
                return new ExceptionHandlingResultModel(validationException.Message, validationData);
            }
            else
            {
                return new ExceptionHandlingResultModel(sourceException.Message, new List<ExceptionOrValidationError>(new[] {
                new ExceptionOrValidationError("",exception.Message)
            }));
            }
        }

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
