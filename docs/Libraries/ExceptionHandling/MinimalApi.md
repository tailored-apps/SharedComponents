# TailoredApps.Shared.ExceptionHandling.MinimalApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.ExceptionHandling.MinimalApi)](https://www.nuget.org/packages/TailoredApps.Shared.ExceptionHandling.MinimalApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Obsługa wyjątków i walidacja żądań dla **minimal API** jako filtry endpointów, na bazie
[ExceptionHandling](index.md): ten sam provider i ten sam model błędu JSON co middleware i filtr MVC. Biblioteka jest osobną paczką;
niczego nie zmienia w istniejących.

- **`WithValidation<TRequest>()`** — przed wywołaniem endpointu uruchamia wszystkie zarejestrowane walidatory FluentValidation dla argumentu
  typu `TRequest`. Błąd to odpowiedź providera (domyślnie 400 z jednym wpisem na pole), a endpoint nie jest wywoływany.
- **`WithExceptionHandling()`** — zamienia wyjątek endpointu (lub całej grupy) na odpowiedź providera. Przydatne, gdy tylko część
  aplikacji ma odpowiadać w ten sposób; dla całej aplikacji wystarcza middleware `app.ConfigureExceptionHandler()`.
- **`ToResult()`** — zamienia `ExceptionHandlingResultModel` na wynik minimal API ze statusem z modelu.

!!! success "NativeAOT"
    Biblioteka jest zgodna z trimmingiem i NativeAOT: typ żądania jest parametrem generycznym, więc filtr nie używa refleksji.
    Sprawdzane natywnie w CI razem z prawdziwym walidatorem FluentValidation
    (zob. [Trimming i NativeAOT](../../Platform/index.md#trimming-i-nativeaot)).

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling.MinimalApi
```

---

## Rejestracja w DI

```csharp
// Program.cs
using FluentValidation;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.Providers;

builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();
```

`AddExceptionHandlingForMinimalApi` rejestruje to samo co `AddExceptionHandlingForWebApi`. Aplikacja, która ma już tę rejestrację dla
kontrolerów, nie powtarza jej.

---

## Przykład użycia

```csharp
using TailoredApps.Shared.ExceptionHandling.MinimalApi;

public record NewProduct(string Name, decimal Price);

public class NewProductValidator : AbstractValidator<NewProduct>
{
    public NewProductValidator()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.Price).GreaterThan(0);
    }
}

var products = app.MapGroup("/api/products").WithExceptionHandling();

// Błędne body -> 400 z polami Name i Price; endpoint nie jest wywoływany
products.MapPost("/", (NewProduct product) => Results.Created($"/api/products/{product.Name}", product))
        .WithValidation<NewProduct>();

// Wyjątek -> odpowiedź providera (np. 404 dla KeyNotFoundException we własnym providerze)
products.MapGet("/{id:int}", (int id) => Find(id) ?? throw new KeyNotFoundException($"Product {id} does not exist."));
```

Odpowiedź przy błędzie walidacji (taka sama jak z middleware):

```json
{
  "message": "Validation failed: ...",
  "errorCode": 400,
  "errors": [
    { "field": "Name", "message": "'Name' must not be empty." },
    { "field": "Price", "message": "'Price' must be greater than '0'." }
  ]
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ExceptionHandlingMinimalApiExtensions.AddExceptionHandlingForMinimalApi<TI, T>` | Metoda ext. | Rejestruje usługi obsługi wyjątków z podanym providerem |
| `ExceptionHandlingMinimalApiExtensions.WithExceptionHandling<TBuilder>()` | Metoda ext. | Filtr wyjątków dla endpointu albo grupy |
| `ExceptionHandlingMinimalApiExtensions.WithValidation<TRequest>()` | Metoda ext. | Filtr walidacji argumentu `TRequest` dla endpointu albo grupy |
| `ExceptionHandlingEndpointFilter` | Filtr endpointu | Wyjątek → model błędu providera; `OperationCanceledException` jest przepuszczany |
| `ValidationEndpointFilter<TRequest>` | Filtr endpointu | Walidatory `IValidator<TRequest>` z DI; brak argumentu lub walidatora → endpoint działa normalnie |
| `ExceptionHandlingResults.ToResult()` | Metoda ext. | `ExceptionHandlingResultModel` → `IResult` (status 400–599, inaczej 500) |

---

## 🔒 Zachowanie

- **Błąd klienta nie jest błędem w logu.** Wyjątek, któremu provider nadał 4xx, filtr zapisuje na poziomie Debug (kategoria
  `TailoredApps.Shared.ExceptionHandling`); 5xx — jako Error.
- **Szczegóły wyjątków nie wyciekają**: decyduje provider; domyślny odpowiada ogólnym komunikatem dla nieoczekiwanych wyjątków.
- **Kilka walidatorów** dla jednego typu: zgłaszane są błędy wszystkich.
- **Kolejność filtrów** to kolejność dodania: `WithExceptionHandling()` dodaj przed pozostałymi, żeby obejmował też ich wyjątki.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.ExceptionHandling.MinimalApi — Instrukcja dla agenta AI

Używasz obsługi wyjątków i walidacji w ASP.NET Core minimal API przez filtry endpointów.

### Rejestracja
```csharp
// Program.cs
builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();

var group = app.MapGroup("/api/products").WithExceptionHandling();
group.MapPost("/", handler).WithValidation<NewProduct>();
```

### Co zostaje skonfigurowane
- IExceptionHandlingService + provider - te same co dla middleware i filtra MVC
- WithValidation<TRequest>(): walidatory IValidator<TRequest> z DI przed endpointem; błąd -> model błędu (domyślnie 400)
- WithExceptionHandling(): wyjątek endpointu lub grupy -> model błędu providera
- ToResult(): ExceptionHandlingResultModel -> IResult

### Konfiguracja
- Brak kluczy konfiguracji. Mapowanie wyjątków na statusy definiuje własny IExceptionHandlingProvider

### Zasady
- Typ żądania podawaj jawnie: WithValidation<NewProduct>() - filtr nie skanuje argumentów refleksją (zgodność z NativeAOT)
- Rejestruj walidatory w DI jako IValidator<T>; bez walidatora filtr przepuszcza żądanie
- Nie łap wyjątków w endpoincie tylko po to, by zwrócić błąd - rzuć wyjątek, a provider nada status
- WithExceptionHandling() dodawaj jako pierwszy filtr; dla całej aplikacji wystarcza app.ConfigureExceptionHandler()
- Nie rejestruj obsługi wyjątków dwa razy: AddExceptionHandlingForWebApi i AddExceptionHandlingForMinimalApi to ta sama rejestracja
- W aplikacji NativeAOT dodaj typy żądań do własnego JsonSerializerContext (wymóg minimal API, nie tej biblioteki)
````
