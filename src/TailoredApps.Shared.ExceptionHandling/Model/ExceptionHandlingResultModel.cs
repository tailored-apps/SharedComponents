using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TailoredApps.Shared.ExceptionHandling.Model
{
    /// <summary>
    /// Represents the standardized error response model returned by the exception handling pipeline.
    /// Contains an error code, a human-readable message, and a list of individual errors or validation failures.
    /// </summary>
    public class ExceptionHandlingResultModel
    {
        /// <summary>
        /// Gets the human-readable error message that describes the overall failure.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the HTTP-style error code associated with this response (e.g. 400, 500).
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// Gets or sets the list of individual errors or validation failures included in this response.
        /// </summary>
        public List<ExceptionOrValidationError> Errors { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionHandlingResultModel"/> with error code 400.
        /// </summary>
        /// <param name="messge">The human-readable error message.</param>
        /// <param name="errors">The collection of individual errors.</param>
        public ExceptionHandlingResultModel(string messge, IEnumerable<ExceptionOrValidationError> errors)
            : this(400, messge, errors)
        {

        }

        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionHandlingResultModel"/> with an explicit error code.
        /// </summary>
        /// <param name="code">The HTTP-style error code.</param>
        /// <param name="message">The human-readable error message.</param>
        /// <param name="errors">The collection of individual errors.</param>
        public ExceptionHandlingResultModel(int code, string message, IEnumerable<ExceptionOrValidationError> errors)
        {
            this.ErrorCode = code;
            this.Message = message;
            this.Errors = new List<ExceptionOrValidationError>(errors);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionHandlingResultModel"/> from an ASP.NET Core
        /// <see cref="ModelStateDictionary"/>, producing a 400-level validation error response.
        /// </summary>
        /// <param name="errors">The model state dictionary containing validation errors.</param>
        public ExceptionHandlingResultModel(ModelStateDictionary errors)
        {
            this.ErrorCode = 400;
            this.Message = "Validation Failed";
            this.Errors = errors.Keys
                .SelectMany(key => errors[key].Errors.Select(x => new ExceptionOrValidationError(key, x.ErrorMessage)))
                .ToList();
        }

        /// <summary>
        /// Serializes this instance to a camel-case JSON string.
        /// </summary>
        /// <returns>A JSON representation of this result model.</returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, typeof(ExceptionHandlingResultModel), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
    }
}
