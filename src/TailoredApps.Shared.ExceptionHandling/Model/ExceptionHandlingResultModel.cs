using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TailoredApps.Shared.ExceptionHandling.Model
{
    public class ExceptionHandlingResultModel
    {

        public string Message { get; }
        public int ErrorCode { get; }
        public List<ExceptionOrValidationError> Errors { get; set; }
        public ExceptionHandlingResultModel(string messge, IEnumerable<ExceptionOrValidationError> errors)
            : this(400, messge, errors)
        {

        }

        public ExceptionHandlingResultModel(int code, string message, IEnumerable<ExceptionOrValidationError> errors)
        {
            this.ErrorCode = code;
            this.Message = message;
            this.Errors = new List<ExceptionOrValidationError>(errors);
        }
        public ExceptionHandlingResultModel(ModelStateDictionary errors)
        {
            this.ErrorCode = 400;
            this.Message = "Validation Failed";
            this.Errors = errors.Keys
                .SelectMany(key => errors[key].Errors.Select(x => new ExceptionOrValidationError(key, x.ErrorMessage)))
                .ToList();
        }
        public override string ToString()
        {
            return JsonSerializer.Serialize(this,typeof(ExceptionHandlingResultModel), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
    }
}
