# TailoredApps.Shared.ExceptionHandling

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.ExceptionHandling)](https://www.nuget.org/packages/TailoredApps.Shared.ExceptionHandling/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Biblioteka standaryzuje obsługę wyjątków w aplikacjach ASP.NET Core Web API. Rozwiązuje problem niespójnych odpowiedzi błędów — zamiast nieobrobionych stack traces lub przypadkowych formatów JSON każdy błąd jest konwertowany na ujednoliconą strukturę `ExceptionHandlingResultModel` (`errorCode`, `message`, `errors[]`).

Dostarcza dwa mechanizmy:

- **Middleware** (`ConfigureExceptionHandler`) — globalny handler przechwytujący nieobsłużone wyjątki dla całej aplikacji
- **Action Filter** (`HandleExceptionAttribute` + `AddExceptionHandlingFilterAttribute`) — zwraca tę samą strukturę dla nieprawidłowego `ModelState` na poziomie kontrolera/akcji

Wbudowany `DefaultExceptionHandlingProvider` mapuje `ValidationException` (FluentValidation) na **400** z błędami per pole, a każdy inny wyjątek na **500** z ogólnym komunikatem. Możesz zaimplementować własny `IExceptionHandlingProvider`, który mapuje wyjątki domenowe na inne kody HTTP.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;

// Wbudowany provider (400 dla walidacji, 500 + ogólny komunikat dla reszty)
builder.Services
    .AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
// ...lub własny provider:
// .AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();

// Opcja A: globalny filter MVC (nieprawidłowy ModelState -> ustrukturyzowane 400)
builder.Services.AddControllers(options =>
{
    options.Filters.AddExceptionHandlingFilterAttribute();
});

// Opcja B: middleware (preferowane dla globalnej obsługi nieobsłużonych wyjątków)
var app = builder.Build();
app.ConfigureExceptionHandler();
```

---

## Przykład użycia

### Własny provider mapujący wyjątki domenowe

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

            // Nigdy nie zwracaj root.Message poza Development: może zawierać connection string, ścieżki lub SQL.
            _ => new ExceptionHandlingResultModel(500,
                includeDetails ? root.Message : "An unexpected error occurred.",
                new[] { new ExceptionOrValidationError("", includeDetails ? root.Message : "An unexpected error occurred.") }),
        };
    }

    public ExceptionHandlingResultModel Response(ModelStateDictionary modelState)
        => new ExceptionHandlingResultModel(modelState);   // 400 "Validation Failed" + błędy pól
}
```

### Wynikowy format JSON odpowiedzi błędu

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

Właściwość `field` jest pomijana (serializacja `WhenWritingNull`), gdy błąd nie dotyczy konkretnego pola.

---

## 🔒 Bezpieczeństwo

- **Szczegóły wyjątków nie wyciekają domyślnie.** `DefaultExceptionHandlingProvider` zwraca `500` z komunikatem `"An unexpected error occurred."` dla wszystkiego, co nie jest błędem walidacji. Komunikat pierwotnego wyjątku jest dołączany tylko wtedy, gdy provider został utworzony z `IHostEnvironment` w środowisku *Development*, lub jawnie przez `new DefaultExceptionHandlingProvider(includeExceptionDetails: true)`. Wcześniej komunikat najgłębszego wyjątku (błędy SQL, nazwy hostów, ścieżki plików) trafiał do każdego anonimowego klienta jako HTTP 400.
- **Poprawne kody statusu.** Awarie serwera to 5xx, więc alerty, load balancery i polityki retry klientów widzą je jako awarie. Middleware ogranicza `ErrorCode` providera do zakresu 400–599, a `ExceptionOccuredResult` honoruje kod z modelu zamiast wymuszać 400.
- **Głośny błąd konfiguracji.** Middleware rozwiązuje `IExceptionHandlingService` przez `GetRequiredService`, więc pominięcie `AddExceptionHandlingForWebApi` daje czytelny wyjątek zamiast pustego 500.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ExceptionHandlingResultModel` | Klasa | Wynikowy obiekt: `ErrorCode` (HTTP), `Message`, `Errors` (lista); konstruktory `(int code, string message, IEnumerable<ExceptionOrValidationError>)`, `(string message, IEnumerable<...>)` = 400, `(ModelStateDictionary)` = 400 |
| `ExceptionOrValidationError` | Klasa | Model błędu: `Field` (nullable) + `Message` |
| `IExceptionHandlingProvider` | Interfejs | Mapuje `Exception` i `ModelStateDictionary` na `ExceptionHandlingResultModel` |
| `DefaultExceptionHandlingProvider` | Klasa | Wbudowany provider: walidacja → 400, reszta → 500 z ogólnym komunikatem; szczegóły tylko w Development |
| `IExceptionHandlingService` | Interfejs | Wyższy poziom — wywołuje provider i zwraca response |
| `ExceptionHandlingConfiguration.AddExceptionHandlingForWebApi` | Metoda ext. | Rejestruje serwis + provider w DI |
| `ExceptionHandlingConfiguration.AddExceptionHandlingFilterAttribute` | Metoda ext. | Dodaje filtr ModelState do MVC |
| `ExceptionMiddlewareExtensions.ConfigureExceptionHandler` | Metoda ext. | Dodaje middleware do pipeline |
| `HandleExceptionAttribute` | Atrybut | Oznacza akcje/kontrolery, których nieprawidłowy ModelState konwertuje filtr |
| `ExceptionOccuredResult` | ObjectResult | Wynik HTTP z modelem; status z `ErrorCode` |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.ExceptionHandling — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.ExceptionHandling do standaryzacji błędów API.

### Rejestracja
```csharp
builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
// Opcja A - middleware (globalnie):
app.ConfigureExceptionHandler();
// Opcja B - filter MVC (nieprawidłowy ModelState):
builder.Services.AddControllers(o => o.Filters.AddExceptionHandlingFilterAttribute());
```

### Co zostaje skonfigurowane
- IExceptionHandlingService + provider (transient), middleware zapisujący JSON z ErrorCode providera
- DefaultExceptionHandlingProvider: ValidationException -> 400 z błędami pól; wszystko inne -> 500 "An unexpected error occurred."

### Własny provider (rzeczywiste API)
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

### Format odpowiedzi
```json
{ "message": "Validation Failed", "errorCode": 400, "errors": [{ "field": "Email", "message": "Required" }, { "message": "Global error" }] }
```

### Zasady
- Nigdy nie zwracaj exception.Message dla nieoczekiwanych błędów poza Development — użyj ogólnego komunikatu i statusu 500
- ErrorCode musi być z zakresu 400–599; middleware mapuje inne wartości na 500
- ExceptionOrValidationError z pustym field (string.Empty) → pole Field = null w JSON (pomijane)
- Provider musi implementować OBA przeciążenia: Response(Exception) i Response(ModelStateDictionary)
- ConfigureExceptionHandler obsługuje WSZYSTKIE nieobsłużone wyjątki — filter konwertuje tylko nieprawidłowy ModelState
- [HandleException] działa na poziomie akcji ORAZ klasy kontrolera
- Dla zagnieżdżonych wyjątków użyj GetBaseException(), by znaleźć pierwotną przyczynę
- AddExceptionHAndlingFilterAttribute (literówka) jest [Obsolete] — używaj AddExceptionHandlingFilterAttribute
```
