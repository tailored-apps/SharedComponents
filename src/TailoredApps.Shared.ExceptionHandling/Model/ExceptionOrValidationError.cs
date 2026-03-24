using System.Text.Json.Serialization;

namespace TailoredApps.Shared.ExceptionHandling.Model
{
    /// <summary>
    /// Represents a single error or validation failure, optionally associated with a specific field.
    /// </summary>
    public class ExceptionOrValidationError
    {
        /// <summary>
        /// Gets the name of the field that caused the error, or <c>null</c> when the error is not field-specific.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Field { get; }

        /// <summary>
        /// Gets the human-readable error message describing what went wrong.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExceptionOrValidationError"/>.
        /// </summary>
        /// <param name="field">The field name associated with the error; pass an empty string for non-field errors.</param>
        /// <param name="errorMessage">The human-readable error message.</param>
        public ExceptionOrValidationError(string field, string errorMessage)
        {
            Field = field != string.Empty ? field : null;
            Message = errorMessage;
        }
    }
}
