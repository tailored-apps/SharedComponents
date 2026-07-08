using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that converts exceptions and model-state errors
    /// into a standardized <see cref="ExceptionHandlingResultModel"/> response.
    /// </summary>
    public interface IExceptionHandlingService
    {
        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from the given exception.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <returns>A result model describing the error.</returns>
        ExceptionHandlingResultModel Response(Exception exception);

        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from an invalid model state.
        /// </summary>
        /// <param name="modelState">The model state dictionary containing validation errors.</param>
        /// <returns>A result model describing the validation errors.</returns>
        ExceptionHandlingResultModel Response(ModelStateDictionary modelState);
    }

    /// <summary>
    /// Defines the contract for a typed exception-handling service that exposes the underlying
    /// <see cref="IExceptionHandlingProvider"/> of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the exception handling provider.</typeparam>
    public interface IExceptionHandlingService<T> : IExceptionHandlingService where T : IExceptionHandlingProvider
    {
        /// <summary>
        /// Gets the concrete exception handling provider used by this service.
        /// </summary>
        public T ExceptionHandlingProvider { get; }
    }
}
