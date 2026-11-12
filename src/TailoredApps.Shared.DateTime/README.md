# TailoredApps.Shared.DateTime

`IDateTimeProvider` gives application code the current date and time as ready-to-use values (`DateTime`,
`DateTimeOffset`, `TimeSpan`) from a clock that can be replaced in tests. The clock is `System.TimeProvider`,
so code that injects `IDateTimeProvider` and code that injects `TimeProvider` (timers, delays) read the same time.

## Installation

```bash
dotnet add package TailoredApps.Shared.DateTime
```

## Registration

```csharp
using TailoredApps.Shared.DateTime;

builder.Services.AddDateTimeProvider();
```

`AddDateTimeProvider()` registers `TimeProvider` (`TimeProvider.System`) and `IDateTimeProvider`
(`DateTimeProvider`) as singletons. There is nothing to configure.

## Usage

```csharp
public class OrderService(IDateTimeProvider clock)
{
    public Order Create(string customerId) => new()
    {
        CustomerId = customerId,
        CreatedAt = clock.OffsetUtcNow,              // DateTimeOffset with a zero offset
        ExpiresAt = clock.OffsetUtcNow.AddDays(30),
    };

    public bool IsExpired(Order order) => order.ExpiresAt < clock.OffsetUtcNow;
}
```

In a test, hand `DateTimeProvider` any `TimeProvider`, for example `FakeTimeProvider` from the
`Microsoft.Extensions.TimeProvider.Testing` package:

```csharp
var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
var service = new OrderService(new DateTimeProvider(time));
var order = service.Create("customer-1");

time.Advance(TimeSpan.FromDays(31));

Assert.True(service.IsExpired(order));
```

| Members of `IDateTimeProvider` | Value |
| --- | --- |
| `UtcNow` / `Now` | `DateTime` in UTC (`Kind == Utc`) / in the local time zone of the clock (`Kind == Local`) |
| `OffsetUtcNow` / `OffsetNow` | `DateTimeOffset` with a zero offset / with the offset of the clock's local time zone |
| `UtcToday` / `Today` | The current date with the time set to midnight, in UTC / in the local time zone |
| `UtcTimeOfDay` / `TimeOfDay` | `TimeSpan` since midnight, in UTC / in the local time zone |

## Good to know

- A `TimeProvider` or `IDateTimeProvider` that is already registered is kept. Register your own `TimeProvider`
  before or after `AddDateTimeProvider()`; `IDateTimeProvider` reads the one the container resolves.
- "Local" means the local time zone of the `TimeProvider` in use, not necessarily the zone of the machine.
- For values that are stored or sent, use `OffsetUtcNow` or `OffsetNow`: a `DateTimeOffset` keeps the offset
  from UTC, a `DateTime` only hints at it through `Kind`.
- `OffsetNow`, `OffsetUtcNow` and `UtcTimeOfDay` have default implementations in the interface, so your own
  implementation or a mock only has to provide `Now`, `UtcNow`, `TimeOfDay`, `Today` and `UtcToday`.
- `UtcTimeOfDaty` is the misspelled, obsolete name of `UtcTimeOfDay`; it still works.
- `new DateTimeProvider()` reads the system clock. `DateTimeProvider.TimeProvider` exposes the clock in use.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/DateTime/
