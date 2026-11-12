# TailoredApps.Shared.MediatR

Five MediatR pipeline behaviours registered in one place: logging, FluentValidation, response caching
(`HybridCache`), fallback and retry with a circuit breaker. A request opts in to caching, fallback or retry by
having a small policy class next to it; handlers stay free of that code.

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR
```

## Registration

```csharp
using TailoredApps.Shared.MediatR.DI;

builder.Services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();                           // the five behaviours and HybridCache
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);   // ICachePolicy<,>, IFallbackHandler<,>, IRetryableRequest<,>

// Validators are not scanned by this package; register the ones you have
builder.Services.AddTransient<IValidator<GetProduct>, GetProductValidator>();
```

Both `RegisterPipelineBehaviors` calls are needed: the overload that takes an assembly only scans it for
policies and fallback handlers, it does not add the behaviours.

## Usage

```csharp
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;

public record GetProduct(int Id) : IRequest<ProductDto>;

public class GetProductCachePolicy : ICachePolicy<GetProduct, ProductDto>
{
    public TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(30);   // default: 5 minutes
    public IReadOnlyCollection<string>? Tags => ["products"];                       // HybridCache.RemoveByTagAsync("products")
}

public class GetProductRetryPolicy : IRetryableRequest<GetProduct, ProductDto>
{
    public int RetryAttempts => 3;                        // default: 1
    public int RetryDelay => 200;                         // milliseconds, default: 250
    public bool RetryWithExponentialBackoff => true;      // default: false
    public int ExceptionsAllowedBeforeCircuitTrip => 5;   // default: 1
}

public class GetProductFallback : IFallbackHandler<GetProduct, ProductDto>
{
    public Task<ProductDto> HandleFallback(GetProduct request, CancellationToken cancellationToken)
        => Task.FromResult(ProductDto.Unavailable);
}
```

## Good to know

- Built on [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a fork of MediatR 12.5.0 under the Apache-2.0 licence, not on the MediatR package: requests and handlers use the `ForkForward.Mediator` namespace and the mediator is registered with `AddMediator(...)`. "MediatR" in the name of this library is its history, not its dependency.
- Order: Logging -> Validation -> Caching -> Fallback -> Retry -> handler. A cached response skips fallback,
  retry and the handler; validation still runs. A response produced by a fallback handler is cached like any other.
- Every response is cached as returned, including `null`, `0` and `false`; a handler that throws caches nothing.
  Concurrent requests with the same key run the handler once. An `AbsoluteExpiration` in the past disables
  caching for that request.
- The default cache key is the request type name plus a SHA-256 hash of the request serialized as JSON. It does
  not contain the caller: for per-user or per-tenant data override `GetCacheKey` and add the identity, or one
  user's response is served to another.
- `HybridCache` keeps entries in process and also uses an `IDistributedCache` when the host registers one.
- `RetryAttempts` is the number of retries after the first failure. When
  `ExceptionsAllowedBeforeCircuitTrip` requests in a row have failed after their retries, requests of that
  type fail with `CircuitBreakerOpenException` (`RequestType`, `RetryAfter`) for 5 seconds; then one trial
  request closes or re-opens the circuit. With the default of `1`, a single failed request opens it. The state
  is shared by all requests of the type in the process.
- A request cancelled by its caller is not retried, does not count towards the circuit and is not sent to the fallback.
- `ValidationBehavior` runs every registered `IValidator<TRequest>` synchronously and throws FluentValidation's
  `ValidationException` with all failures.
- `LoggingBehavior` logs start and finish at Debug and failures at Error, with the request type name and a
  correlation id. The request object itself is never logged.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/MediatR/
