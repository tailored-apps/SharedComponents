# TailoredApps.Shared.MediatR.Caching

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.Caching)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.Caching/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

A lightweight package defining `ICachableRequest<TResponse>` — a marker interface for MediatR requests whose results should be cached. A request implementing this interface provides a `GetCacheKey()` method that generates a unique cache key for that particular query instance.

This is an alternative caching approach compared to `ICachePolicy<TRequest, TResponse>` from `TailoredApps.Shared.MediatR` — simpler when cache key logic is straightforward and can live in the request itself.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.Caching
```

---

## Przykład użycia

```csharp
using TailoredApps.Shared.MediatR.Caching;
using MediatR;

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

!!! note "Integracja z CachingBehavior"
    Aby cache'owanie działało, potrzebujesz `CachingBehavior` z pakietu `TailoredApps.Shared.MediatR` w pipeline. `ICachableRequest` to marker interface — sam w sobie nie uruchamia cache'owania.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ICachableRequest<TResponse>` | Interfejs | Rozszerza `IRequest<TResponse>`; wymaga `GetCacheKey()` |
| `ICachableRequest.GetCacheKey()` | Metoda | Zwraca unikalny klucz cache dla tej instancji requestu |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.MediatR.Caching — Instrukcja dla agenta AI

Używasz ICachableRequest do oznaczania requestów MediatR, których wyniki mają być cache'owane.

### Użycie
```csharp
public class GetProductQuery : ICachableRequest<ProductDto>
{
    public int Id { get; set; }
    public string GetCacheKey() => $"product:{Id}";
}
```

### Zasady
- ICachableRequest<TResponse> rozszerza IRequest<TResponse>
- GetCacheKey() musi zwracać unikalny klucz dla tej kombinacji parametrów
- Sam interfejs nie cache'uje — potrzebny CachingBehavior z TailoredApps.Shared.MediatR
- Alternatywa: ICachePolicy<TRequest, TResponse> — klucz w osobnej klasie policy
```
