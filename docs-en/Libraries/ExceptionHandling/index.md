# TailoredApps.Shared.ExceptionHandling

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.ExceptionHandling)](https://www.nuget.org/packages/TailoredApps.Shared.ExceptionHandling/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library standardizes exception handling in ASP.NET Core Web API applications. It solves the problem of inconsistent error responses: instead of raw stack traces or ad-hoc JSON, every error is converted to a unified `ExceptionHandlingResultModel` (`errorCode`, `message`, `errors[]`).

It provides two mechanisms:

- **Middleware** (`ConfigureExceptionHandler`) - a global handler that intercepts unhandled exceptions for the whole application
- **Action filter** (`HandleExceptionAttribute` + `AddExceptionHandlingFilterAttribute`) - returns the same structure for invalid `ModelState` at controller/action level

The shipped `DefaultExceptionHandlingProvider` maps FluentValidation `ValidationException` to **400** with per-field errors and every other exception to **500** with a generic message. You can implement your own `IExceptionHandlingProvider` to map domain exceptions to other HTTP codes.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;

// Built-in provider (400 for validation, 500 + generic message otherwise)
builder.Services
    .AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
// ...or your own provider:
// .AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();

// Option A: global MVC filter (invalid ModelState -> structured 400)
builder.Services.AddControllers(options =>
{
    options.Filters.AddExceptionHandlingFilterAttribute();
});

// Option B: middleware (preferred for global handling of unhandled exceptions)
var app = builder.Build();
app.ConfigureExceptionHandler();
```

---

## Usage example

### Custom provider mapping domain exceptions

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

public class MyExceptionHandlingProvider : IExceptionHandlingProvider
{
    private readonly bool includeDetails;

    public MyExceptionHandlingProvider(IHostEnvironment env) => includeDetails = env.IsDevelopment();

    public ExceptionHandlingResultModel Response(Exception exception)
    {
        var root = exception.GetBaseException();
        return root switch
        {
            ValidationException ve => new ExceptionHandlingResultModel(400, ve.Message,
                ve.Errors.Select(e => new ExceptionOrValidationError(e.PropertyName, e.ErrorMessage))),

            NotFoundException nf => new ExceptionHandlingResultModel(404, nf.Message,
                new[] { new ExceptionOrValidationError("", nf.Message) }),

            UnauthorizedAccessException => new ExceptionHandlingResultModel(401, "Unauthorized",
                new[] { new ExceptionOrValidationError("", "Unauthorized") }),

            // Never echo root.Message outside Development: it may contain connection strings, paths or SQL.
            _ => new ExceptionHandlingResultModel(500,
                includeDetails ? root.Message : "An unexpected error occurred.",
                new[] { new ExceptionOrValidationError("", includeDetails ? root.Message : "An unexpected error occurred.") }),
        };
    }

    public ExceptionHandlingResultModel Response(ModelStateDictionary modelState)
        => new ExceptionHandlingResultModel(modelState);   // 400 "Validation Failed" + field errors
}
```

### Resulting JSON error response

```json
{
  "message": "Validation Failed",
  "errorCode": 400,
  "errors": [
    { "field": "Email", "message": "Email address is required" },
    { "message": "Name must not be empty" }
  ]
}
```

The `field` property is omitted (`WhenWritingNull`) when the error is not tied to a specific field.

---

## 🔒 Security

- **No exception details leak by default.** `DefaultExceptionHandlingProvider` returns `500` with `"An unexpected error occurred."` for anything that is not a validation failure. The root-cause message is included only when the provider is created with `IHostEnvironment` in the *Development* environment, or explicitly with `new DefaultExceptionHandlingProvider(includeExceptionDetails: true)`. Previously the innermost exception message (SQL errors, host names, file paths) was returned to every anonymous caller as HTTP 400.
- **Correct status codes.** Server failures are 5xx, so alerting, load balancers and client retry policies see them as such. The middleware clamps the provider's `ErrorCode` to 400-599 and `ExceptionOccuredResult` honours the model's code instead of forcing 400.
- **Fail loudly on mis-configuration.** The middleware resolves `IExceptionHandlingService` with `GetRequiredService`, so forgetting `AddExceptionHandlingForWebApi` produces a clear exception instead of a blank 500.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `ExceptionHandlingResultModel` | Class | Result object: `ErrorCode` (HTTP), `Message`, `Errors` (list); constructors `(int code, string message, IEnumerable<ExceptionOrValidationError>)`, `(string message, IEnumerable<...>)` = 400, `(ModelStateDictionary)` = 400 |
| `ExceptionOrValidationError` | Class | Error model: `Field` (nullable) + `Message` |
| `IExceptionHandlingProvider` | Interface | Maps `Exception` and `ModelStateDictionary` to `ExceptionHandlingResultModel` |
| `DefaultExceptionHandlingProvider` | Class | Built-in provider: validation -> 400, other -> 500 generic; details only in Development |
| `IExceptionHandlingService` | Interface | Higher level - calls the provider and returns the response |
| `ExceptionHandlingConfiguration.AddExceptionHandlingForWebApi` | Ext. method | Registers the service + provider in DI |
| `ExceptionHandlingConfiguration.AddExceptionHandlingFilterAttribute` | Ext. method | Adds the ModelState filter to MVC |
| `ExceptionMiddlewareExtensions.ConfigureExceptionHandler` | Ext. method | Adds the middleware to the pipeline |
| `HandleExceptionAttribute` | Attribute | Marks actions/controllers whose invalid ModelState is converted by the filter |
| `ExceptionOccuredResult` | ObjectResult | HTTP result carrying the model; status from `ErrorCode` |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.ExceptionHandling - AI agent instructions

You are using TailoredApps.Shared.ExceptionHandling to standardize API errors.

### Registration
```csharp
builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
// Option A - middleware (global):
app.ConfigureExceptionHandler();
// Option B - MVC filter (invalid ModelState):
builder.Services.AddControllers(o => o.Filters.AddExceptionHandlingFilterAttribute());
```

### What gets configured
- IExceptionHandlingService + the provider (transient), middleware writing JSON with the provider's ErrorCode
- DefaultExceptionHandlingProvider: ValidationException -> 400 with field errors; anything else -> 500 "An unexpected error occurred."

### Custom provider (real API)
```csharp
public class MyProvider : IExceptionHandlingProvider
{
    public ExceptionHandlingResultModel Response(Exception ex) => ex.GetBaseException() switch
    {
        NotFoundException nf => new ExceptionHandlingResultModel(404, nf.Message, [new ExceptionOrValidationError("", nf.Message)]),
        ValidationException ve => new ExceptionHandlingResultModel(400, ve.Message,
            ve.Errors.Select(e => new ExceptionOrValidationError(e.PropertyName, e.ErrorMessage))),
        _ => new ExceptionHandlingResultModel(500, "An unexpected error occurred.", [new ExceptionOrValidationError("", "An unexpected error occurred.")])
    };
    public ExceptionHandlingResultModel Response(ModelStateDictionary ms) => new(ms);
}
```

### Response format
```json
{ "message": "Validation Failed", "errorCode": 400, "errors": [{ "field": "Email", "message": "Required" }, { "message": "Global error" }] }
```

### Rules
- Never return exception.Message for unexpected errors outside Development - use a generic message and status 500
- ErrorCode must be 400-599; the middleware maps anything else to 500
- ExceptionOrValidationError with an empty field (string.Empty) -> Field = null in JSON (omitted)
- The provider must implement BOTH overloads: Response(Exception) and Response(ModelStateDictionary)
- ConfigureExceptionHandler handles ALL unhandled exceptions; the filter only converts invalid ModelState
- [HandleException] works at both the action AND the controller class level
- For nested exceptions use GetBaseException() to find the root cause
- AddExceptionHAndlingFilterAttribute (typo) is [Obsolete] - use AddExceptionHandlingFilterAttribute
```
