# TailoredApps.Shared.MediatR.Caching

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.Caching)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.Caching/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

A lightweight package defining `ICachableRequest<TResponse>` — a contract for MediatR requests that name their own cache key. A request implementing this interface provides a `GetCacheKey()` method that generates a unique cache key for that particular query instance.

The package contains the contract only: it has no services and nothing to register. `CachingBehavior` from `TailoredApps.Shared.MediatR` does **not** read `ICachableRequest<TResponse>` — it caches a request only when an `ICachePolicy<TRequest, TResponse>` is registered for it. Use the interface to keep the key logic in the request itself, and add a cache policy that returns that key (shown below) when the response is to be cached by that behavior.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.Caching
```

---

## Usage example

```csharp
using TailoredApps.Shared.MediatR.Caching;
using MediatR;

// Request with built-in cache key logic
public class GetUserProfileQuery : ICachableRequest<UserProfileDto>
{
    public int UserId { get; set; }
    public string Language { get; set; } = "pl";

    // Unique key that takes the query parameters into account
    public string GetCacheKey() => $"user-profile:{UserId}:{Language}";
}

// Handler — standard MediatR
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

### Caching the request with `CachingBehavior`

```csharp
using TailoredApps.Shared.MediatR.Interfaces.Caching;

// The policy is what CachingBehavior looks for; it hands back the key the request defines
public class GetUserProfileQueryCachePolicy : ICachePolicy<GetUserProfileQuery, UserProfileDto>
{
    public string GetCacheKey(GetUserProfileQuery request) => request.GetCacheKey();
}
```

!!! note "Integration with CachingBehavior"
    `ICachableRequest` is a marker interface — on its own it does not trigger caching, and `CachingBehavior` from the `TailoredApps.Shared.MediatR` package does not look at it. A request is cached only when an `ICachePolicy<TRequest, TResponse>` is registered for it (`PipelineRegistration.RegisterPipelineBehaviors(assembly)` scans for policies). Without a policy the request above runs its handler every time.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `ICachableRequest<TResponse>` | Interface | Extends `IRequest<TResponse>`; requires `GetCacheKey()` |
| `ICachableRequest.GetCacheKey()` | Method | Returns a unique cache key for this request instance |

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR.Caching - instructions for an AI agent

You are using ICachableRequest to give MediatR requests their own cache key.

### Registration
Nothing to register: the package contains one interface, no services and no configuration.

### Usage
```csharp
public class GetProductQuery : ICachableRequest<ProductDto>
{
    public int Id { get; set; }
    public string GetCacheKey() => $"product:{Id}";
}

// Needed for CachingBehavior (TailoredApps.Shared.MediatR) to cache the request:
public class GetProductQueryCachePolicy : ICachePolicy<GetProductQuery, ProductDto>
{
    public string GetCacheKey(GetProductQuery request) => request.GetCacheKey();
}
```

### Rules
- ICachableRequest<TResponse> extends IRequest<TResponse>
- GetCacheKey() must return a unique key for this combination of parameters
- The interface alone does not cache, and CachingBehavior from TailoredApps.Shared.MediatR does not read it
- CachingBehavior caches only requests that have a registered ICachePolicy<TRequest, TResponse> — add a policy that returns request.GetCacheKey()
````
