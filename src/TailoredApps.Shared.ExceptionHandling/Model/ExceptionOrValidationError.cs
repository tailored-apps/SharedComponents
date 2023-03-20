using System.Text.Json.Serialization;

namespace TailoredApps.Shared.ExceptionHandling.Model
{
    public class ExceptionOrValidationError
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Field { get; }
        public string Message { get; }
        public ExceptionOrValidationError(string field, string errorMessage)
        {
            Field = field != string.Empty ? field : null;
            Message = errorMessage;
        }
    }
}