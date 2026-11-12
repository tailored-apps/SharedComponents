# TailoredApps.Shared.ExceptionHandling

One error format for an ASP.NET Core Web API. Unhandled exceptions and invalid model state are turned into the
same response model, `ExceptionHandlingResultModel` (`message`, `errorCode`, `errors`), by an
`IExceptionHandlingProvider` that decides the status code and the text for each exception.

## Installation

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling
```

## Registration

```csharp
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;

builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddControllers(options => options.Filters.AddExceptionHandlingFilterAttribute());

var app = builder.Build();
app.ConfigureExceptionHandler();   // unhandled exceptions -> JSON error response
```

`DefaultExceptionHandlingProvider` answers a FluentValidation `ValidationException` with 400 and one error per
failed property, and every other exception with 500 and the text `An unexpected error occurred.`

## Usage

A provider of your own maps domain exceptions to other status codes:

```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;

public class ApiExceptionHandlingProvider : IExceptionHandlingProvider
{
    private readonly DefaultExceptionHandlingProvider defaults = new();

    public ExceptionHandlingResultModel Response(Exception exception)
        => exception.GetBaseException() is KeyNotFoundException
            ? new ExceptionHandlingResultModel(404, "Not found.", new[] { new ExceptionOrValidationError("", "Not found.") })
            : defaults.Response(exception);

    public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => defaults.Response(modelState);
}

// Program.cs
builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, ApiExceptionHandlingProvider>();
```

The response written by the middleware:

```json
{
  "message": "Not found.",
  "errorCode": 404,
  "errors": [
    { "message": "Not found." }
  ]
}
```

An error tied to a field also carries `"field"`; the property is left out when the field name is empty.

## Good to know

- `DefaultExceptionHandlingProvider` returns the real exception message only when it is created with an
  `IHostEnvironment` that is in Development, or with `includeExceptionDetails: true`. Its parameterless
  constructor never does. Do the same in your own provider: exception messages can contain connection strings,
  paths and SQL.
- The middleware (`ConfigureExceptionHandler`) is what handles exceptions. The filter only replaces the result
  of an action with invalid model state, and only on actions or controllers marked with `[HandleException]`.
- The HTTP status is the model's `ErrorCode`. The middleware answers 500 and the filter result 400 when the
  code is outside 400-599. If the response has already started, the middleware writes nothing.
- An exception the provider answers with 4xx (validation, a missing resource) is the client's mistake: the middleware
  logs no "unhandled exception" error for it and adds no `error.type` tag to the request metric; it is logged at Debug
  in the `TailoredApps.Shared.ExceptionHandling` category. A 5xx answer is logged as an error, as before. A 404 the
  provider chose is sent as such.
- `ExceptionHandlingResultModel(message, errors)` and `ExceptionHandlingResultModel(modelState)` use code 400;
  the latter has the message `Validation Failed` and one error per model-state entry.
- The middleware needs `AddExceptionHandlingForWebApi`: it resolves `IExceptionHandlingService` and throws
  when the service is not registered. `AddExceptionHAndlingFilterAttribute` is the obsolete, misspelled name
  of `AddExceptionHandlingFilterAttribute`.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/ExceptionHandling/
