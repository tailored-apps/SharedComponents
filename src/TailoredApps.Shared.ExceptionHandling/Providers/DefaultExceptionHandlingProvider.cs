using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Shared.ExceptionHandling.Providers
{
    /// <summary>
    /// Default implementation of <see cref="IExceptionHandlingProvider"/> that handles
    /// FluentValidation <see cref="ValidationException"/> instances and general exceptions,
    /// converting them into <see cref="ExceptionHandlingResultModel"/> responses.
    /// </summary>
    /// <remarks>
    /// Validation failures are reported as HTTP 400 with the individual field errors.
    /// Any other exception is reported as HTTP 500 with a generic message. The real exception
    /// message is included only when the provider was created for the Development environment
    /// (or explicitly with <c>includeExceptionDetails = true</c>), because exception messages
    /// routinely contain connection strings, file paths, SQL fragments and internal host names.
    /// </remarks>
    public class DefaultExceptionHandlingProvider : IExceptionHandlingProvider
    {
        /// <summary>The message returned to clients for unexpected server errors.</summary>
        public const string GenericErrorMessage = "An unexpected error occurred.";

        private readonly bool includeExceptionDetails;

        /// <summary>
        /// Creates a provider that never discloses exception details to clients (safe default).
        /// </summary>
        public DefaultExceptionHandlingProvider() : this(includeExceptionDetails: false)
        {
        }

        /// <summary>
        /// Creates a provider that discloses exception details only when the host runs in the
        /// Development environment.
        /// </summary>
        /// <param name="hostEnvironment">The host environment.</param>
        public DefaultExceptionHandlingProvider(IHostEnvironment hostEnvironment)
            : this(hostEnvironment?.IsDevelopment() ?? false)
        {
        }

        /// <summary>
        /// Creates a provider with an explicit choice about exception-detail disclosure.
        /// </summary>
        /// <param name="includeExceptionDetails">
        /// When <c>true</c>, the root-cause exception message is returned to the client for
        /// non-validation errors. Use only in trusted, non-production environments.
        /// </param>
        public DefaultExceptionHandlingProvider(bool includeExceptionDetails)
        {
            this.includeExceptionDetails = includeExceptionDetails;
        }

        /// <summary>
        /// Creates an <see cref="ExceptionHandlingResultModel"/> from the given exception.
        /// If the root cause is a FluentValidation <see cref="ValidationException"/>,
        /// individual validation errors are mapped with status 400; otherwise a 500 response
        /// with a generic message is produced.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <returns>A result model describing the error.</returns>
        public ExceptionHandlingResultModel Response(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            var sourceException = exception.GetBaseException();
            if (sourceException is ValidationException validationException)
            {
                var validationData = validationException.Errors
                    .DistinctBy(x => new { x.PropertyName, x.ErrorMessage })
                    .Select(x => new ExceptionOrValidationError(x.PropertyName, x.ErrorMessage));
                return new ExceptionHandlingResultModel(400, validationException.Message, validationData);
            }

            var message = includeExceptionDetails ? sourceException.Message : GenericErrorMessage;
            return new ExceptionHandlingResultModel(500, message, new List<ExceptionOrValidationError>
            {
                new ExceptionOrValidationError("", message)
            });
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
}
