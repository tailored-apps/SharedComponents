# TailoredApps.Shared.MediatR.Caching

A contracts-only package with one interface, `ICachableRequest<TResponse>`: a MediatR request that names its
own cache key. It lets a request type say how its response is identified in a cache without referencing any
caching implementation.

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.Caching
```

## Registration

Nothing to register: the package has no dependency-injection extension and no services.

## Usage

```csharp
using TailoredApps.Shared.MediatR.Caching;

public class GetUserProfile : ICachableRequest<UserProfileDto>
{
    public int UserId { get; init; }
    public string Language { get; init; } = "en";

    public string GetCacheKey() => $"user-profile:{UserId}:{Language}";
}
```

`ICachableRequest<TResponse>` extends `IRequest<TResponse>`, so the request is sent and handled like any other
MediatR request.

## Good to know

- Built on [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a fork of MediatR 12.5.0 under the Apache-2.0 licence, not on the MediatR package: requests and handlers use the `ForkForward.Mediator` namespace and the mediator is registered with `AddMediator(...)`. "MediatR" in the name of this library is its history, not its dependency.
- The interface is a marker with a key; nothing in this package stores or reads a cache.
- `CachingBehavior` in `TailoredApps.Shared.MediatR` does not look at `ICachableRequest<TResponse>`. It caches
  a request only when an `ICachePolicy<TRequest, TResponse>` is registered for it. To cache the request above
  with that behaviour, add a policy that returns the request's own key:

  ```csharp
  using TailoredApps.Shared.MediatR.Interfaces.Caching;

  public class GetUserProfileCachePolicy : ICachePolicy<GetUserProfile, UserProfileDto>
  {
      public string GetCacheKey(GetUserProfile request) => request.GetCacheKey();
  }
  ```

- The key is all that identifies a cached response. Put every value that changes the response into it,
  including the user or tenant when the data is per user.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/MediatR/Caching/
