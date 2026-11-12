# TailoredApps.Shared.MediatR

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Biblioteka dostarcza gotowy zestaw **pipeline behaviors** dla MediatR, które pokrywają najczęstsze potrzeby aplikacji enterprise: logowanie, walidację, cache'owanie, fallback i retry. Zamiast ręcznie implementować te cross-cutting concerns w każdym handlerze, rejestrujesz je raz przez `PipelineRegistration` i masz je dla wszystkich requestów.

Behaviory działają w kolejności: **Logging → Validation → Caching → Fallback → Retry → Handler**.

Biblioteka wspiera mechanizm auto-discovery (przez Scrutor) — cache policies, fallback handlers i retry konfiguracje są skanowane i rejestrowane automatycznie ze wskazanego assembly.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.MediatR.DI;

// Rejestracja MediatR
builder.Services.AddMediator(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var pipeline = new PipelineRegistration(builder.Services);

// 1. Pięć pipeline behaviors + HybridCache
pipeline.RegisterPipelineBehaviors();

// 2. Skan assembly w poszukiwaniu implementacji ICachePolicy<,>, IFallbackHandler<,> i IRetryableRequest<,>
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);

// Biblioteka nie skanuje walidatorów — zarejestruj te, które masz
builder.Services.AddTransient<IValidator<GetProductQuery>, GetProductQueryValidator>();
```

Potrzebne są oba wywołania `RegisterPipelineBehaviors`. Przeciążenie bez parametrów rejestruje behaviory (i `HybridCache`), ale żadnych polityk; przeciążenie przyjmujące assembly tylko je skanuje i nie dodaje behaviorów. Bez pierwszego wywołania w pipeline nic nie działa; bez drugiego żaden request nie jest cache'owany, ponawiany ani obsługiwany przez fallback, bo behaviory nie znajdują dla niego polityki.

`RegisterPipelineBehaviors()` rejestruje też `HybridCache`, w którym `CachingBehavior` przechowuje odpowiedzi. Sam z siebie to cache w pamięci procesu; gdy w kontenerze jest `IDistributedCache` (np. Redis), `HybridCache` używa go automatycznie jako poziomu współdzielonego między instancjami:

```csharp
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");
```

!!! info "Zmiany w 3.0 (wersja testowa)"
    Biblioteki `TailoredApps.Shared.MediatR*` korzystają z [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator) — forka MediatR 12.5.0 na licencji Apache-2.0 — zamiast pakietu MediatR. Typy i zachowanie są te same; w aplikacji zamień `using MediatR;` na `using ForkForward.Mediator;` oraz `AddMediatR(...)` na `AddMediator(...)`. Szczegóły: [Aktualizacja do 3.0](../../Migration/v3.md).

!!! info "Zmiany w 2.0"
    `CachingBehavior` korzysta z `HybridCache`. Interfejs `ICache` i klasa `Cache` zostały usunięte razem z zależnością od Newtonsoft.Json — usuń własną rejestrację `ICache`. Odpowiedzi są serializowane przez System.Text.Json, a `ICachePolicy.SlidingExpiration` nie jest już obsługiwane (wpis żyje do czasu wygaśnięcia). Po aktualizacji wyczyść współdzielony cache lub zmień prefiks kluczy: wpisy zapisane przez 1.x mają inny format.

    Gdy host zarejestruje `IDistributedCache` (np. Redis), odpowiedzi są współdzielone między instancjami aplikacji: instancja, która nie ma wpisu w pamięci, odczytuje go z cache rozproszonego bez wywoływania handlera. Gdy cache rozproszony jest niedostępny, żądanie obsługuje handler — awaria cache nie zatrzymuje żądań.

---

## Przykład użycia

### Request + Handler

```csharp
// Request
public class GetProductQuery : IRequest<ProductDto>
{
    public int ProductId { get; set; }
}

// Validator (uruchamiany przez ValidationBehavior, gdy jest zarejestrowany w DI jako IValidator<GetProductQuery>)
public class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
    }
}

// Handler
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    private readonly IProductRepository _repo;

    public GetProductQueryHandler(IProductRepository repo) => _repo = repo;

    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(request.ProductId, ct);
        return product?.ToDto() ?? throw new NotFoundException($"Product {request.ProductId} not found");
    }
}
```

### Cache Policy dla requestu

```csharp
public class GetProductQueryCachePolicy : ICachePolicy<GetProductQuery, ProductDto>
{
    public string GetCacheKey(GetProductQuery request)
        => $"product:{request.ProductId}";

    public TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromHours(1);
    public TimeSpan? LocalCacheExpiration => TimeSpan.FromMinutes(5);
    public IReadOnlyCollection<string>? Tags => ["products"];
}
```

### Fallback Handler

```csharp
public class GetProductQueryFallback : IFallbackHandler<GetProductQuery, ProductDto>
{
    public Task<ProductDto> HandleFallback(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Zwróć wartość domyślną, gdy handler rzuci (wyjątek nie jest przekazywany)
        return Task.FromResult(ProductDto.Empty);
    }
}
```

### Konfiguracja retry

```csharp
public class GetProductQueryRetry : IRetryableRequest<GetProductQuery, ProductDto>
{
    public int RetryAttempts => 3;                        // ponowienia po pierwszym błędzie; domyślnie 1
    public int RetryDelay => 200;                         // milisekundy; domyślnie 250
    public bool RetryWithExponentialBackoff => true;      // domyślnie false
    public int ExceptionsAllowedBeforeCircuitTrip => 5;   // domyślnie 1
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `LoggingBehavior<TRequest, TResponse>` | Pipeline Behavior | Loguje czas wykonania i wyjątki; correlation ID per request |
| `ValidationBehavior<TRequest, TResponse>` | Pipeline Behavior | Wykonuje wszystkie `IValidator<TRequest>` (FluentValidation) |
| `CachingBehavior<TRequest, TResponse>` | Pipeline Behavior | Cache'uje odpowiedź w `HybridCache` zgodnie z `ICachePolicy<TRequest, TResponse>`; równoległe identyczne żądania wykonują handler raz |
| `FallbackBehavior<TRequest, TResponse>` | Pipeline Behavior | Przy wyjątku wywołuje `IFallbackHandler<TRequest, TResponse>` |
| `RetryBehavior<TRequest, TResponse>` | Pipeline Behavior | Ponawia request zgodnie z `IRetryableRequest<TRequest, TResponse>`; po `ExceptionsAllowedBeforeCircuitTrip` kolejnych nieudanych requestach otwiera circuit breaker (współdzielony per typ requestu) i przez 5 s odrzuca wywołania wyjątkiem `CircuitBreakerOpenException` (`RequestType`, `RetryAfter`); potem jedno żądanie próbne zamyka lub ponownie otwiera obwód. Bez zewnętrznych bibliotek |
| `PipelineRegistration` | Klasa | `RegisterPipelineBehaviors()` rejestruje pięć behaviorów i `HybridCache`; `RegisterPipelineBehaviors(assembly)` tylko skanuje assembly w poszukiwaniu polityk cache, handlerów fallbacku i konfiguracji retry |
| `IPipelineRegistration` | Interfejs | Kontrakt PipelineRegistration |
| `ICachePolicy<TRequest, TResponse>` | Interfejs | Konfiguracja cache: klucz, czas życia (`AbsoluteExpirationRelativeToNow`, domyślnie 5 min, lub `AbsoluteExpiration`), `LocalCacheExpiration`, `Tags` |
| `IFallbackHandler<TRequest, TResponse>` | Interfejs | Handler fallbacku przy wyjątku: `HandleFallback(request, cancellationToken)` |
| `IRetryableRequest<TRequest, TResponse>` | Interfejs | Konfiguracja retry dla requestu: `RetryAttempts` (1), `RetryDelay` w ms (250), `RetryWithExponentialBackoff` (false), `ExceptionsAllowedBeforeCircuitTrip` (1) |
| `HybridCache` | Klasa (.NET) | Magazyn odpowiedzi używany przez `CachingBehavior`; rejestrowany przez `RegisterPipelineBehaviors()`. Unieważnianie: `RemoveAsync(key)`, `RemoveByTagAsync(tag)` |

---

## 🔒 Bezpieczeństwo

- **Klucz cache nie zawiera danych żądania.** Domyślny `ICachePolicy.GetCacheKey` buduje klucz jako `{TypRequestu.FullName}:{SHA-256 z JSON żądania}` (`CacheKeyGenerator.Generate`). Poprzedni format `Prop:ToString()` sprawiał, że żądania różniące się tylko zawartością kolekcji (`CustomerIds = [1]` vs `[2]`) dostawały ten sam klucz i zwracały cudze dane.
- **Klucz nie zawiera tożsamości użytkownika.** Dla danych per użytkownik/tenant nadpisz `GetCacheKey` i dodaj prefiks z identyfikatorem wywołującego — inaczej odpowiedź jednego użytkownika trafi do drugiego.
- **`LoggingBehavior` nie serializuje żądania.** W scope logowania trafia tylko nazwa typu i correlation id; hasła, tokeny i binaria z requestu nie lądują w logach.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.MediatR z pipeline behaviors w projekcie .NET.

### Rejestracja
```csharp
builder.Services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();                         // pięć behaviorów + HybridCache
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly); // skan: ICachePolicy<,>, IFallbackHandler<,>, IRetryableRequest<,>
builder.Services.AddTransient<IValidator<MyQuery>, MyQueryValidator>(); // walidatory nie są skanowane
```

### Kolejność behaviors
Logging → Validation → Caching → Fallback → Retry → Handler

### Walidacja
```csharp
// Uruchamiana dla każdego IValidator<MyQuery> zarejestrowanego w DI — rzuca ValidationException gdy błąd
public class MyQueryValidator : AbstractValidator<MyQuery>
{
    public MyQueryValidator() { RuleFor(x => x.Id).GreaterThan(0); }
}
```

### Cache Policy
```csharp
public class MyCachePolicy : ICachePolicy<MyQuery, MyResponse>
{
    public string GetCacheKey(MyQuery r) => $"my:{r.Id}";
    public TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromHours(1);
    public TimeSpan? LocalCacheExpiration => TimeSpan.FromMinutes(5);
    public IReadOnlyCollection<string>? Tags => ["products"];
}
```

### Fallback i retry
```csharp
public class MyFallback : IFallbackHandler<MyQuery, MyResponse>
{
    public Task<MyResponse> HandleFallback(MyQuery request, CancellationToken cancellationToken)
        => Task.FromResult(MyResponse.Empty);
}

public class MyRetry : IRetryableRequest<MyQuery, MyResponse>
{
    public int RetryAttempts => 3;                       // domyślnie 1
    public int ExceptionsAllowedBeforeCircuitTrip => 5;  // domyślnie 1
}
```

### Zasady
- Wywołaj oba: RegisterPipelineBehaviors() i RegisterPipelineBehaviors(assembly) — pierwsze dodaje behaviory, drugie tylko skanuje polityki
- ValidationBehavior uruchamia instancje IValidator<TRequest> zarejestrowane w DI — biblioteka nie rejestruje walidatorów, zrób to sam
- Aby cache działał, zaimplementuj ICachePolicy<TRequest, TResponse> w skanowanym assembly (albo zarejestruj ją samodzielnie)
- IFallbackHandler ma jedną metodę: HandleFallback(request, cancellationToken) — wyjątek nie jest do niej przekazywany
- LoggingBehavior loguje na poziomie DEBUG — włącz odpowiedni log level
- Każdy request ma unikalne correlation ID w logach
- Domyślny klucz cache to hash treści żądania; dla danych per użytkownik nadpisz GetCacheKey i dodaj identyfikator użytkownika/tenanta
- LoggingBehavior loguje tylko nazwę requestu i correlation id — dane żądania loguj jawnie w handlerze, nigdy całe obiekty z hasłami
- Cache opiera się na HybridCache (rejestruje go RegisterPipelineBehaviors): nie rejestruj ICache — ten interfejs nie istnieje w 2.0
- Każda odpowiedź jest cache'owana tak, jak została zwrócona — także null, 0 i false; równoległe identyczne żądania wykonują handler raz
- Czas życia wpisu ustawiaj przez AbsoluteExpirationRelativeToNow (domyślnie 5 minut) albo AbsoluteExpiration; SlidingExpiration nie jest obsługiwane
- Typ odpowiedzi w cache musi dać się serializować przez System.Text.Json; dla innych typów zarejestruj IHybridCacheSerializer<T>
- Aby współdzielić cache między instancjami, zarejestruj IDistributedCache (np. Redis) — HybridCache użyje go automatycznie
- Żądanie anulowane przez wywołującego nie jest ponawiane, nie otwiera circuit breakera i nie trafia do fallbacku
- Otwarty obwód zgłasza CircuitBreakerOpenException (w 1.x: BrokenCircuitException z Polly); odpowiedz 503 i nagłówkiem Retry-After z właściwości RetryAfter
- Biblioteka nie zależy od Polly ani Newtonsoft.Json — nie dodawaj ich, by skonfigurować retry lub cache
````
