# TailoredApps.Shared.Mediator.Caching

A contracts-only package with one interface, `ICachableRequest<TResponse>`: a mediator request that names its
own cache key. It lets a request type say how its response is identified in a cache without referencing any
caching implementation.

## Installation

```bash
dotnet add package TailoredApps.Shared.Mediator.Caching
```

## Registration

Nothing to register: the package has no dependency-injection extension and no services.

## Usage

```csharp
using TailoredApps.Shared.Mediator.Caching;

public class GetUserProfile : ICachableRequest<UserProfileDto>
{
    public int UserId { get; init; }
    public string Language { get; init; } = "en";

    public string GetCacheKey() => $"user-profile:{UserId}:{Language}";
}
```

`ICachableRequest<TResponse>` extends `IRequest<TResponse>`, so the request is sent and handled like any other
mediator request.

## Good to know

- Built on [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a fork of MediatR 12.5.0 under the Apache-2.0 licence, not on the MediatR package: requests and handlers use the `ForkForward.Mediator` namespace and the mediator is registered with `AddMediator(...)`. In 2.x this package was named `TailoredApps.Shared.MediatR.Caching`.
- The interface is a marker with a key; nothing in this package stores or reads a cache.
- `CachingBehavior` in `TailoredApps.Shared.Mediator` does not look at `ICachableRequest<TResponse>`. It caches
  a request only when an `ICachePolicy<TRequest, TResponse>` is registered for it. To cache the request above
  with that behaviour, add a policy that returns the request's own key:

  ```csharp
  using TailoredApps.Shared.Mediator.Interfaces.Caching;

  public class GetUserProfileCachePolicy : ICachePolicy<GetUserProfile, UserProfileDto>
  {
      public string GetCacheKey(GetUserProfile request) => request.GetCacheKey();
  }
  ```

- The key is all that identifies a cached response. Put every value that changes the response into it,
  including the user or tenant when the data is per user.
- Safe for trimming and NativeAOT. A policy that returns `request.GetCacheKey()` needs no reflection, unlike the default cache key.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/Mediator/Caching/
