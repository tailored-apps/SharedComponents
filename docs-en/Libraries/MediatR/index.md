# TailoredApps.Shared.MediatR

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a ready-made set of **pipeline behaviors** for MediatR that cover the most common enterprise application needs: logging, validation, caching, fallback, and retry. Instead of manually implementing these cross-cutting concerns in every handler, register them once via `PipelineRegistration` and they apply to all requests.

Behaviors execute in order: **Logging → Validation → Caching → Fallback → Retry → Handler**.

The library supports auto-discovery (via Scrutor) — cache policies, fallback handlers, and retry configurations are automatically scanned and registered from the specified assembly.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.MediatR.DI;

// Register MediatR
builder.Services.AddMediator(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var pipeline = new PipelineRegistration(builder.Services);

// 1. The five pipeline behaviors + HybridCache
pipeline.RegisterPipelineBehaviors();

// 2. Scan the assembly for ICachePolicy<,>, IFallbackHandler<,> and IRetryableRequest<,> implementations
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);

// Validators are not scanned by the library - register the ones you have
builder.Services.AddTransient<IValidator<GetProductQuery>, GetProductQueryValidator>();
```

Both `RegisterPipelineBehaviors` calls are needed. The parameterless overload registers the behaviors (and `HybridCache`) but no policies; the overload that takes an assembly only scans it and does not add the behaviors. Without the first call nothing runs in the pipeline; without the second no request is cached, retried or answered by a fallback, because the behaviors find no policy for it.

`RegisterPipelineBehaviors()` also registers the `HybridCache` in which `CachingBehavior` keeps responses. On its own it is an in-process cache; when an `IDistributedCache` (for example Redis) is registered, `HybridCache` uses it automatically as the level shared between instances:

```csharp
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");
```

!!! info "Changes in 3.0 (test phase)"
    The `TailoredApps.Shared.MediatR*` libraries use [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator) - a fork of MediatR 12.5.0 under the Apache-2.0 licence - instead of the MediatR package. The types and the behaviour are the same; in the application replace `using MediatR;` with `using ForkForward.Mediator;` and `AddMediatR(...)` with `AddMediator(...)`. Details: [Upgrading to 3.0](../../Migration/v3.md). From ForkForward.Mediator 13 the library also works in an application published with NativeAOT - see [Trimming and NativeAOT](#trimming-and-nativeaot).

!!! info "Changes in 2.0"
    `CachingBehavior` uses `HybridCache`. The `ICache` interface and the `Cache` class are removed together with the Newtonsoft.Json dependency — delete your own `ICache` registration. Responses are serialized with System.Text.Json, and `ICachePolicy.SlidingExpiration` is no longer supported (an entry lives until it expires). After upgrading, clear the shared cache or change the key prefix: entries written by 1.x have a different format.

    When the host registers an `IDistributedCache` (Redis, for example), responses are shared between application instances: an instance without the entry in memory reads it from the distributed cache without calling the handler. When the distributed cache cannot be reached, the handler answers the request - a cache outage does not stop requests.

---

## Usage example

### Request + Handler

```csharp
// Request
public class GetProductQuery : IRequest<ProductDto>
{
    public int ProductId { get; set; }
}

// Validator (run by ValidationBehavior once it is registered in DI as IValidator<GetProductQuery>)
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

### Cache Policy for a request

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
        // Return a default value when the handler throws (the exception is not passed in)
        return Task.FromResult(ProductDto.Empty);
    }
}
```

### Retry configuration

```csharp
public class GetProductQueryRetry : IRetryableRequest<GetProductQuery, ProductDto>
{
    public int RetryAttempts => 3;                        // retries after the first failure; default 1
    public int RetryDelay => 200;                         // milliseconds; default 250
    public bool RetryWithExponentialBackoff => true;      // default false
    public int ExceptionsAllowedBeforeCircuitTrip => 5;   // default 1
}
```

### Trimming and NativeAOT

From version **3.0** the library declares that it is safe for trimming and NativeAOT (on ForkForward.Mediator 13). In an application
published with `<PublishAot>true</PublishAot>` or `<PublishTrimmed>true</PublishTrimmed>` nothing can be found by scanning an assembly, so
handlers, behaviors and policies are registered by name:

```csharp
// Program.cs
builder.Services.AddMediator(cfg =>
{
    cfg.AddRequestHandler<GetProductQuery, ProductDto, GetProductQueryHandler>();
    cfg.AddRequestHandler<CountProductsQuery, int, CountProductsQueryHandler>();
});

new PipelineRegistration(builder.Services)
    .AddPipelineBehaviors<GetProductQuery, ProductDto>()              // the five behaviors, closed for this request
    .AddPipelineBehaviorsWithoutCaching<CountProductsQuery, int>()    // four of them, no cache
    .AddCachePolicy<GetProductQuery, ProductDto, GetProductQueryCachePolicy>()
    .AddFallbackHandler<GetProductQuery, ProductDto, GetProductQueryFallback>()
    .AddRetryPolicy<GetProductQuery, ProductDto, GetProductQueryRetry>()
    .AddCacheSerialization(AppJsonContext.Default);

builder.Services.AddTransient<IValidator<GetProductQuery>, GetProductQueryValidator>();

[JsonSerializable(typeof(ProductDto))]         // the responses kept in the cache
[JsonSerializable(typeof(GetProductQuery))]    // the requests a cache key is computed from
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

| Ordinary application (JIT) | Trimmed or NativeAOT application |
|---|---|
| `cfg.RegisterServicesFromAssembly(...)` | `cfg.AddRequestHandler<TRequest, TResponse, THandler>()` for every handler |
| `RegisterPipelineBehaviors()` | `AddPipelineBehaviors<TRequest, TResponse>()` for every request; `AddPipelineBehaviorsWithoutCaching<,>()` for a request that is never cached |
| `RegisterPipelineBehaviors(assembly)` | `AddCachePolicy<,,>()`, `AddFallbackHandler<,,>()`, `AddRetryPolicy<,,>()` |
| The default cache key (a hash of the request JSON, built by reflection) | Your own `GetCacheKey` in the policy, or `CacheKeyGenerator.Generate(request, AppJsonContext.Default.GetProductQuery)` |
| Cached responses serialized by reflection | `AddCacheSerialization(AppJsonContext.Default)`, with the response types in `[JsonSerializable]` |

- **Open behaviors and value types.** `RegisterPipelineBehaviors()` works natively for requests whose response is a reference type. For a
  response that is a value type (`int`, `bool`, `Guid`, a struct) the container throws `InvalidOperationException` natively:
  *Unable to create a generic service for type 'IPipelineBehavior`2[…,System.Int32]' because 'System.Int32' is a ValueType*. That is why
  such an application registers the behaviors closed, per request. Pick one form for the whole application: `RegisterPipelineBehaviors()`
  together with `AddPipelineBehaviors<,>()` would run the behaviors twice.
- **Scanning is marked.** `RegisterPipelineBehaviors(assembly)` carries `RequiresUnreferencedCode` and `RequiresDynamicCode`: the analyzer
  reports `IL2026` / `IL3050` at the call instead of letting policies disappear silently when the application is trimmed.
- **The cache key.** The default key serializes the request by reflection. When reflection-based JSON is switched off,
  `CacheKeyGenerator.Generate(Type, object)` throws `InvalidOperationException` instead of building a key from whatever survived trimming.
  Override `GetCacheKey` in the policy or use the overload that takes a `JsonTypeInfo`. Do not assume that the two overloads give the same
  key for the same request.
- **HybridCache.** Publishing an application that caches responses prints `IL2026` / `IL3050` warnings from the
  `Microsoft.Extensions.Caching.Hybrid` assembly: they are about its fallback JSON serializer, which is not used for the types covered by
  `AddCacheSerialization`. The cache itself works natively - an application of its own checks that in CI
  (`tests/TailoredApps.Shared.AotSmoke.Caching`). An application that caches nothing and registers every request with
  `AddPipelineBehaviorsWithoutCaching<,>()` publishes without warnings.
- **A request without a response** (`IRequest`) does not go through the behaviors of this library (they require `IRequest<TResponse>`);
  only its handler is registered: `cfg.AddRequestHandler<TRequest, THandler>()`.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `LoggingBehavior<TRequest, TResponse>` | Pipeline Behavior | Logs the execution time (Debug), handler failures (Error), validation refusals and cancellations (Information); correlation ID per request |
| `ValidationBehavior<TRequest, TResponse>` | Pipeline Behavior | Runs every `IValidator<TRequest>` (FluentValidation) |
| `CachingBehavior<TRequest, TResponse>` | Pipeline Behavior | Caches the response in `HybridCache` according to `ICachePolicy<TRequest, TResponse>`; concurrent identical requests run the handler once |
| `FallbackBehavior<TRequest, TResponse>` | Pipeline Behavior | On an exception calls `IFallbackHandler<TRequest, TResponse>` |
| `RetryBehavior<TRequest, TResponse>` | Pipeline Behavior | Retries the request per `IRetryableRequest<TRequest, TResponse>`; after `ExceptionsAllowedBeforeCircuitTrip` consecutive failed requests it opens a circuit breaker (shared per request type) and rejects calls with `CircuitBreakerOpenException` (`RequestType`, `RetryAfter`) for 5 s; then a single trial request closes or re-opens the circuit. No third-party library involved |
| `PipelineRegistration` | Class | `RegisterPipelineBehaviors()` registers the five behaviors and `HybridCache`; `RegisterPipelineBehaviors(assembly)` only scans the assembly for cache policies, fallback handlers and retry configurations |
| `IPipelineRegistration` | Interface | PipelineRegistration contract |
| `PipelineRegistration` - registration without scanning | Class | `AddPipelineBehaviors<TRequest, TResponse>()` - the five behaviors closed for one request (and `HybridCache`); `AddPipelineBehaviorsWithoutCaching<,>()` - four of them, no cache; `AddCachePolicy<,,>()`, `AddFallbackHandler<,,>()`, `AddRetryPolicy<,,>()` - policies by name; `AddCacheSerialization(JsonSerializerContext)` - cached responses serialized with generated metadata. Each returns `PipelineRegistration`, so the calls chain |
| `JsonContextCacheSerializerFactory` | Class | An `IHybridCacheSerializerFactory` on the metadata of a `JsonSerializerContext`; it answers for the types the context knows and leaves the rest to the other factories. Registered by `AddCacheSerialization` |
| `CacheKeyGenerator` | Static class | `Generate(Type, object)` - the key `{type}:{SHA-256 of the request JSON}` built by reflection (throws `InvalidOperationException` in a trimmed application); `Generate<TRequest>(request, JsonTypeInfo<TRequest>)` - the same on generated metadata |
| `ICachePolicy<TRequest, TResponse>` | Interface | Cache configuration: key, lifetime (`AbsoluteExpirationRelativeToNow`, 5 minutes by default, or `AbsoluteExpiration`), `LocalCacheExpiration`, `Tags` |
| `IFallbackHandler<TRequest, TResponse>` | Interface | Fallback handler for an exception: `HandleFallback(request, cancellationToken)` |
| `IRetryableRequest<TRequest, TResponse>` | Interface | Retry configuration for a request: `RetryAttempts` (1), `RetryDelay` in ms (250), `RetryWithExponentialBackoff` (false), `ExceptionsAllowedBeforeCircuitTrip` (1) |
| `HybridCache` | Class (.NET) | Response store used by `CachingBehavior`; registered by `RegisterPipelineBehaviors()`. Invalidation: `RemoveAsync(key)`, `RemoveByTagAsync(tag)` |

---

## 🔒 Security

- **The cache key carries no request data.** The default `ICachePolicy.GetCacheKey` builds `{RequestType.FullName}:{SHA-256 of the request JSON}` (`CacheKeyGenerator.Generate`). The previous `Prop:ToString()` format gave requests that differed only in collection contents (`CustomerIds = [1]` vs `[2]`) the same key, returning another caller's data.
- **The key does not include the caller's identity.** For per-user or per-tenant data override `GetCacheKey` and prefix the key with the caller id, otherwise one user's response is served to another.
- **`LoggingBehavior` does not serialize the request.** Only the request type name and a correlation id go into the logging scope; passwords, tokens and binary payloads never reach the logs.
- **A validation refusal is not an error.** A `ValidationException` is logged at Information level (`rejected by validation …; invalid: Id`)
  with the property names only - validation messages can quote the values the client sent. A request cancelled by its caller is logged as
  Information too; every other handler exception as Error. The exception is always rethrown.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR - instructions for an AI agent

You are using the TailoredApps.Shared.MediatR library with pipeline behaviors in a .NET project.

### Registration
```csharp
builder.Services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();                         // the five behaviors + HybridCache
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly); // scans for ICachePolicy<,>, IFallbackHandler<,>, IRetryableRequest<,>
builder.Services.AddTransient<IValidator<MyQuery>, MyQueryValidator>(); // validators are not scanned
```

### Order of behaviors
Logging → Validation → Caching → Fallback → Retry → Handler

### Validation
```csharp
// Runs for every IValidator<MyQuery> registered in DI — throws ValidationException on an error
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

### Fallback and retry
```csharp
public class MyFallback : IFallbackHandler<MyQuery, MyResponse>
{
    public Task<MyResponse> HandleFallback(MyQuery request, CancellationToken cancellationToken)
        => Task.FromResult(MyResponse.Empty);
}

public class MyRetry : IRetryableRequest<MyQuery, MyResponse>
{
    public int RetryAttempts => 3;                       // default 1
    public int ExceptionsAllowedBeforeCircuitTrip => 5;  // default 1
}
```

### Rules
- Call both RegisterPipelineBehaviors() and RegisterPipelineBehaviors(assembly): the first adds the behaviors, the second only scans for policies
- ValidationBehavior runs the IValidator<TRequest> instances registered in DI - the library does not register validators, do it yourself
- For the cache to work, implement ICachePolicy<TRequest, TResponse> in the scanned assembly (or register it yourself)
- IFallbackHandler has one method: HandleFallback(request, cancellationToken) - the exception is not passed to it
- LoggingBehavior: start and finish at Debug, validation refusals and cancellations at Information, handler failures at Error - do not count validation failures from Error entries
- Every request has a unique correlation ID in the logs
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
- An application with PublishAot or PublishTrimmed: scan nothing - cfg.AddRequestHandler<TRequest, TResponse, THandler>() for handlers, AddPipelineBehaviors<TRequest, TResponse>() (or AddPipelineBehaviorsWithoutCaching) for every request instead of RegisterPipelineBehaviors(), AddCachePolicy / AddFallbackHandler / AddRetryPolicy instead of RegisterPipelineBehaviors(assembly)
- In such an application override GetCacheKey (or use CacheKeyGenerator.Generate(request, jsonTypeInfo)) and call AddCacheSerialization(context) with the response types in [JsonSerializable]; the default key throws InvalidOperationException there
- Do not combine RegisterPipelineBehaviors() with AddPipelineBehaviors<,>() in one application - the behaviors would run twice
````
