# TailoredApps.Shared.ExceptionHandling

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.ExceptionHandling)](https://www.nuget.org/packages/TailoredApps.Shared.ExceptionHandling/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library standardizes exception handling in ASP.NET Core Web API applications. It solves the problem of inconsistent error responses — instead of raw stack traces or random JSON formats, every error is converted to a unified `ExceptionOrValidationError` structure.

Provides two mechanisms:

- **Middleware** (`ConfigureExceptionHandler`) — global handler intercepting exceptions for the entire application
- **Action Filter** (`HandleExceptionAttribute`) — decorative approach at controller/action level

You can define your own `IExceptionHandlingProvider` that maps specific exception types to HTTP codes and error messages.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.ExceptionHandling.WebApiCore;

// Rejestracja serwisu + własnego handlera
builder.Services
    .AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();

// Opcja A: Globalny filter MVC
builder.Services.AddControllers(options =>
{
    options.Filters.AddExceptionHAndlingFilterAttribute();
});

// Opcja B: Middleware (preferowane dla globalnej obsługi)
var app = builder.Build();
app.ConfigureExceptionHandler();
```

---

## Przykład użycia

### Własny provider mapujący wyjątki

```csharp
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;

public class MyExceptionHandlingProvider : IExceptionHandlingProvider
{
    public ExceptionHandlingResponse Response(Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => new ExceptionHandlingResponse
            {
                ErrorCode = 422,
                Errors = validationEx.Errors
                    .Select(e => new ExceptionOrValidationError(e.PropertyName, e.ErrorMessage))
                    .ToList()
            },

            NotFoundException notFoundEx => new ExceptionHandlingResponse
            {
                ErrorCode = 404,
                Errors = new[] { new ExceptionOrValidationError("", notFoundEx.Message) }
            },

            UnauthorizedException => new ExceptionHandlingResponse
            {
                ErrorCode = 401,
                Errors = new[] { new ExceptionOrValidationError("", "Unauthorized") }
            },

            _ => new ExceptionHandlingResponse
            {
                ErrorCode = 500,
                Errors = new[] { new ExceptionOrValidationError("", "Internal server error") }
            }
        };
    }
}
```

### Wynikowy format JSON odpowiedzi błędu

```json
{
  "errors": [
    {
      "field": "Email",
      "message": "Email address is required"
    },
    {
      "message": "Name must not be empty"
    }
  ]
}
```

Właściwość `field` jest pomijana (serializacja `WhenWritingNull`) gdy błąd nie dotyczy konkretnego pola.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ExceptionOrValidationError` | Klasa | Model błędu: `Field` (nullable) + `Message` |
| `IExceptionHandlingProvider` | Interfejs | Mapuje `Exception` na `ExceptionHandlingResponse` |
| `IExceptionHandlingService` | Interfejs | Wyższy poziom — wywołuje provider i zwraca response |
| `ExceptionHandlingConfiguration.AddExceptionHandlingForWebApi` | Metoda ext. | Rejestruje handler + filter w DI |
| `ExceptionMiddlewareExtensions.ConfigureExceptionHandler` | Metoda ext. | Dodaje middleware do pipeline |
| `HandleExceptionAttribute` | Action Filter | Dekoracyjna obsługa wyjątku na poziomie akcji |
| `ExceptionHandlingResponse` | Klasa | Wynikowy obiekt: `ErrorCode` (HTTP) + `Errors` (lista) |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.ExceptionHandling — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.ExceptionHandling do standaryzacji błędów API.

### Rejestracja
```csharp
builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyProvider>();
// Opcja A - middleware (globalnie):
app.ConfigureExceptionHandler();
// Opcja B - filter MVC:
builder.Services.AddControllers(o => o.Filters.AddExceptionHAndlingFilterAttribute());
```

### Implementacja własnego providera
```csharp
public class MyProvider : IExceptionHandlingProvider
{
    public ExceptionHandlingResponse Response(Exception ex) => ex switch
    {
        NotFoundException => new() { ErrorCode = 404, Errors = [new("", ex.Message)] },
        ValidationException ve => new() { ErrorCode = 422, Errors = ve.Errors
            .Select(e => new ExceptionOrValidationError(e.PropertyName, e.ErrorMessage)).ToList() },
        _ => new() { ErrorCode = 500, Errors = [new("", "Internal server error")] }
    };
}
```

### Format odpowiedzi
```json
{ "errors": [{ "field": "Email", "message": "Required" }, { "message": "Global error" }] }
```

### Zasady
- ExceptionOrValidationError z pustym field (string.Empty) → pole Field = null w JSON (pomijane)
- Zawsze implementuj własny IExceptionHandlingProvider mapujący domeny wyjątki
- Middleware ConfigureExceptionHandler obsługuje WSZYSTKIE wyjątki — filter tylko opakowane
```
