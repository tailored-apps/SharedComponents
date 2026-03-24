using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore
{
    /// <summary>
    /// Generic implementation of <see cref="IExceptionHandlingService{T}"/> that delegates
    /// exception and model-state handling to an <see cref="IExceptionHandlingProvider"/> of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the exception handling provider.</typeparam>
    public class ExceptionHandlingService<T> : IExceptionHandlingService<T> where T : IExceptionHandlingProvider
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionHandlingService{T}"/>.
        /// </summary>
        /// <param name="exceptionHandlingProvider">The provider used to produce error response models.</param>
        public ExceptionHandlingService(T exceptionHandlingProvider)
        {
            this.ExceptionHandlingProvider = exceptionHandlingProvider;
        }

        /// <summary>
        /// Gets the concrete exception handling provider used by this service.
        /// </summary>
        public T ExceptionHandlingProvider { get; private set; }

        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from the given exception
        /// by delegating to <see cref="ExceptionHandlingProvider"/>.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <returns>A result model describing the error.</returns>
        public ExceptionHandlingResultModel Response(Exception exception)
        {
            return ExceptionHandlingProvider.Response(exception);
        }

        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from an invalid model state
        /// by delegating to <see cref="ExceptionHandlingProvider"/>.
        /// </summary>
        /// <param name="modelState">The model state dictionary containing validation errors.</param>
        /// <returns>A result model describing the validation errors.</returns>
        public ExceptionHandlingResultModel Response(ModelStateDictionary modelState)
        {
            return ExceptionHandlingProvider.Response(modelState);
        }
    }
}
