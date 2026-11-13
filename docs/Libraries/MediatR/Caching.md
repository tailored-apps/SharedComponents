# TailoredApps.Shared.MediatR.Caching

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.Caching)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.Caching/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Lekki pakiet definiujący `ICachableRequest<TResponse>` — kontrakt dla requestów MediatR, które same podają swój klucz cache. Request implementujący ten interfejs dostarcza metodę `GetCacheKey()`, która generuje unikalny klucz cache dla danej instancji zapytania.

Pakiet zawiera wyłącznie kontrakt: nie ma serwisów ani niczego do zarejestrowania. `CachingBehavior` z pakietu `TailoredApps.Shared.MediatR` **nie** czyta `ICachableRequest<TResponse>` — cache'uje request tylko wtedy, gdy jest dla niego zarejestrowana `ICachePolicy<TRequest, TResponse>`. Używaj interfejsu, żeby trzymać logikę klucza w samym requeście, a gdy odpowiedź ma być cache'owana przez ten behavior, dodaj politykę cache zwracającą ten klucz (przykład niżej).

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.Caching
```

---

## Przykład użycia

```csharp
using TailoredApps.Shared.MediatR.Caching;
using ForkForward.Mediator;

// Request z wbudowaną logiką cache key
public class GetUserProfileQuery : ICachableRequest<UserProfileDto>
{
    public int UserId { get; set; }
    public string Language { get; set; } = "pl";

    // Unikalny klucz uwzględniający parametry zapytania
    public string GetCacheKey() => $"user-profile:{UserId}:{Language}";
}

// Handler — standardowy MediatR
public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserProfileDto>
{
    private readonly IUserRepository _repo;

    public GetUserProfileQueryHandler(IUserRepository repo) => _repo = repo;

    public async Task<UserProfileDto> Handle(GetUserProfileQuery request, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(request.UserId, ct);
        return user.ToProfileDto(request.Language);
    }
}
```

### Cache'owanie requestu przez `CachingBehavior`

```csharp
using TailoredApps.Shared.MediatR.Interfaces.Caching;

// To polityki szuka CachingBehavior; zwraca ona klucz zdefiniowany przez request
public class GetUserProfileQueryCachePolicy : ICachePolicy<GetUserProfileQuery, UserProfileDto>
{
    public string GetCacheKey(GetUserProfileQuery request) => request.GetCacheKey();
}
```

!!! note "Integracja z CachingBehavior"
    `ICachableRequest` to marker interface — sam w sobie nie uruchamia cache'owania, a `CachingBehavior` z pakietu `TailoredApps.Shared.MediatR` nie bierze go pod uwagę. Request jest cache'owany tylko wtedy, gdy jest dla niego zarejestrowana `ICachePolicy<TRequest, TResponse>` (polityki skanuje `PipelineRegistration.RegisterPipelineBehaviors(assembly)`). Bez polityki powyższy request za każdym razem wykonuje handler.

!!! info "Trimming i NativeAOT"
    Pakiet zawiera sam kontrakt i jest zgodny z trimmingiem i NativeAOT. W takiej aplikacji polityka zwracająca `request.GetCacheKey()`
    jest też najprostszym sposobem na klucz cache: domyślny klucz `ICachePolicy` wymaga refleksji. Politykę rejestruje się po nazwie
    (`AddCachePolicy<,,>()`) — zob. [MediatR: Trimming i NativeAOT](index.md#trimming-i-nativeaot).

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ICachableRequest<TResponse>` | Interfejs | Rozszerza `IRequest<TResponse>`; wymaga `GetCacheKey()` |
| `ICachableRequest.GetCacheKey()` | Metoda | Zwraca unikalny klucz cache dla tej instancji requestu |

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR.Caching — Instrukcja dla agenta AI

Używasz ICachableRequest, żeby requesty MediatR same podawały swój klucz cache.

### Rejestracja
Nie ma nic do zarejestrowania: pakiet zawiera jeden interfejs, bez serwisów i bez konfiguracji.

### Użycie
```csharp
public class GetProductQuery : ICachableRequest<ProductDto>
{
    public int Id { get; set; }
    public string GetCacheKey() => $"product:{Id}";
}

// Potrzebne, żeby CachingBehavior (TailoredApps.Shared.MediatR) cache'ował request:
public class GetProductQueryCachePolicy : ICachePolicy<GetProductQuery, ProductDto>
{
    public string GetCacheKey(GetProductQuery request) => request.GetCacheKey();
}
```

### Zasady
- ICachableRequest<TResponse> rozszerza IRequest<TResponse>
- GetCacheKey() musi zwracać unikalny klucz dla tej kombinacji parametrów
- Sam interfejs nie cache'uje, a CachingBehavior z TailoredApps.Shared.MediatR go nie czyta
- CachingBehavior cache'uje tylko requesty z zarejestrowaną ICachePolicy<TRequest, TResponse> — dodaj politykę zwracającą request.GetCacheKey()
- Aplikacja z PublishAot: politykę rejestruj po nazwie — new PipelineRegistration(services).AddCachePolicy<TRequest, TResponse, TPolicy>() — skanowanie nie jest tam dostępne
````
