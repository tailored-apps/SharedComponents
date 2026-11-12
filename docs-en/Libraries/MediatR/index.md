# TailoredApps.Shared.MediatR

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a ready-made set of **pipeline behaviors** for MediatR that cover the most common enterprise application needs: logging, validation, caching, fallback, and retry. Instead of manually implementing these cross-cutting concerns in every handler, register them once via `PipelineRegistration` and they apply to all requests.

Behaviors execute in order: **Logging → Validation → Caching → Fallback → Retry → Handler**.

The library supports auto-discovery (via Scrutor) — cache policies, fallback handlers, and retry configurations are automatically scanned and registered from the specified assembly.

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
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Rejestracja pipeline behaviors
var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();

// Opcjonalnie: auto-discovery cache policies, fallback, retry z assembly
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);
```

`RegisterPipelineBehaviors()` also registers the `HybridCache` in which `CachingBehavior` keeps responses. On its own it is an in-process cache; when an `IDistributedCache` (for example Redis) is registered, `HybridCache` uses it automatically as the level shared between instances:

```csharp
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");
```

!!! info "Changes in 2.0"
    `CachingBehavior` uses `HybridCache`. The `ICache` interface and the `Cache` class are removed together with the Newtonsoft.Json dependency — delete your own `ICache` registration. Responses are serialized with System.Text.Json, and `ICachePolicy.SlidingExpiration` is no longer supported (an entry lives until it expires). After upgrading, clear the shared cache or change the key prefix: entries written by 1.x have a different format.

    When the host registers an `IDistributedCache` (Redis, for example), responses are shared between application instances: an instance without the entry in memory reads it from the distributed cache without calling the handler. When the distributed cache cannot be reached, the handler answers the request - a cache outage does not stop requests.

---

## Przykład użycia

### Request + Handler

```csharp
// Request
public class GetProductQuery : IRequest<ProductDto>
{
    public int ProductId { get; set; }
}

// Validator (automatycznie przechwycony przez ValidationBehavior)
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
    public Task<ProductDto> HandleFallbackAsync(
        GetProductQuery request,
        Exception exception,
        CancellationToken ct)
    {
        // Zwróć cached/default wartość gdy handler rzuci
        return Task.FromResult(ProductDto.Empty);
    }
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `LoggingBehavior<TRequest, TResponse>` | Pipeline Behavior | Loguje czas wykonania i wyjątki; correlation ID per request |
| `ValidationBehavior<TRequest, TResponse>` | Pipeline Behavior | Wykonuje wszystkie `IValidator<TRequest>` (FluentValidation) |
| `CachingBehavior<TRequest, TResponse>` | Pipeline Behavior | Caches the response in `HybridCache` according to `ICachePolicy<TRequest, TResponse>`; concurrent identical requests run the handler once |
| `FallbackBehavior<TRequest, TResponse>` | Pipeline Behavior | Przy wyjątku wywołuje `IFallbackHandler<TRequest, TResponse>` |
| `RetryBehavior<TRequest, TResponse>` | Pipeline Behavior | Retries the request per `IRetryableRequest<TRequest, TResponse>`; after `ExceptionsAllowedBeforeCircuitTrip` consecutive failed requests it opens a circuit breaker (shared per request type) and rejects calls with `CircuitBreakerOpenException` (`RequestType`, `RetryAfter`) for 5 s; then a single trial request closes or re-opens the circuit. No third-party library involved |
| `PipelineRegistration` | Klasa | Rejestruje wszystkie behaviors + auto-discovery z assembly |
| `IPipelineRegistration` | Interfejs | Kontrakt PipelineRegistration |
| `ICachePolicy<TRequest, TResponse>` | Interface | Cache configuration: key, lifetime (`AbsoluteExpirationRelativeToNow`, 5 minutes by default, or `AbsoluteExpiration`), `LocalCacheExpiration`, `Tags` |
| `IFallbackHandler<TRequest, TResponse>` | Interfejs | Handler fallbacku przy wyjątku |
| `IRetryableRequest<TRequest, TResponse>` | Interfejs | Konfiguracja retry dla requestu |
| `HybridCache` | Class (.NET) | Response store used by `CachingBehavior`; registered by `RegisterPipelineBehaviors()`. Invalidation: `RemoveAsync(key)`, `RemoveByTagAsync(tag)` |

---

## 🔒 Security

- **The cache key carries no request data.** The default `ICachePolicy.GetCacheKey` builds `{RequestType.FullName}:{SHA-256 of the request JSON}` (`CacheKeyGenerator.Generate`). The previous `Prop:ToString()` format gave requests that differed only in collection contents (`CustomerIds = [1]` vs `[2]`) the same key, returning another caller's data.
- **The key does not include the caller's identity.** For per-user or per-tenant data override `GetCacheKey` and prefix the key with the caller id, otherwise one user's response is served to another.
- **`LoggingBehavior` does not serialize the request.** Only the request type name and a correlation id go into the logging scope; passwords, tokens and binary payloads never reach the logs.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.MediatR — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.MediatR z pipeline behaviors w projekcie .NET.

### Rejestracja
```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly); // auto-discovery
```

### Kolejność behaviors
Logging → Validation → Caching → Fallback → Retry → Handler

### Walidacja (automatyczna)
```csharp
// Validator automatycznie przechwycony — rzuca ValidationException gdy błąd
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

### Zasady
- Wszystkie FluentValidation validators są automatycznie wykrywane przez DI
- Aby cache działał, zaimplementuj ICachePolicy<TRequest, TResponse> i zarejestruj (auto-discovery)
- LoggingBehavior loguje na poziomie DEBUG — włącz odpowiedni log level
- Każdy request ma unikalne correlation ID w logach
- The default cache key is a hash of the request content; for per-user data override GetCacheKey and add the user/tenant id
- LoggingBehavior logs only the request name and correlation id - log request fields explicitly in the handler, never whole objects with secrets
- Caching is built on HybridCache (registered by RegisterPipelineBehaviors): do not register ICache - that interface does not exist in 2.0
- Every response is cached as returned - including null, 0 and false; concurrent identical requests run the handler once
- Set an entry's lifetime with AbsoluteExpirationRelativeToNow (5 minutes by default) or AbsoluteExpiration; SlidingExpiration is not supported
- A cached response type must be serializable with System.Text.Json; register an IHybridCacheSerializer<T> for other types
- To share the cache between instances register an IDistributedCache (for example Redis) - HybridCache uses it automatically
- A request cancelled by its caller is not retried, does not open the circuit breaker and is not sent to the fallback
- An open circuit throws CircuitBreakerOpenException (in 1.x: Polly's BrokenCircuitException); answer 503 with a Retry-After header taken from its RetryAfter property
- The library depends on neither Polly nor Newtonsoft.Json - do not add them to configure retries or caching
```
