# TailoredApps.Shared.DateTime

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.DateTime)](https://www.nuget.org/packages/TailoredApps.Shared.DateTime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

!!! warning "Obsolete since 2.0 - use `System.TimeProvider`"
    .NET has its own clock abstraction: [`System.TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider).
    `IDateTimeProvider` and `DateTimeProvider` are marked `[Obsolete]` (diagnostic **`TAS0001`**, a warning, not an error)
    and will be removed in 3.0. Inject `TimeProvider` in new code.

The library provides the `IDateTimeProvider` interface and the `DateTimeProvider` implementation - a clock that can be replaced
in tests instead of calling `DateTime.Now` directly.

Since 2.0 `DateTimeProvider` reads its time from a `TimeProvider`. That makes a gradual migration possible: you register one
`TimeProvider`, and code still using `IDateTimeProvider` and code already moved to `TimeProvider` see **the same clock** and the
same time zone - in tests with `FakeTimeProvider` as well.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.DateTime
```

New projects do not need the package - `TimeProvider` is part of .NET.

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.DateTime;

// The target state: only the clock from .NET.
builder.Services.AddSingleton(TimeProvider.System);

// During the migration: an IDateTimeProvider that reads the same clock.
#pragma warning disable TAS0001
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
#pragma warning restore TAS0001
```

The container picks the `DateTimeProvider(TimeProvider)` constructor when a `TimeProvider` is registered; otherwise the
parameterless one, which reads the system clock.

---

## Usage example

### Target code (`TimeProvider`)

```csharp
public class OrderService
{
    private readonly TimeProvider _time;

    public OrderService(TimeProvider time) => _time = time;

    public Order CreateOrder(string customerId, decimal amount)
    {
        var now = _time.GetUtcNow();
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            CreatedAt = now,
            ExpiresAt = now.AddDays(30),
        };
    }

    public bool IsOrderExpired(Order order) => order.ExpiresAt < _time.GetUtcNow();
}
```

### Unit test (`FakeTimeProvider`)

```bash
dotnet add package Microsoft.Extensions.TimeProvider.Testing
```

```csharp
using Microsoft.Extensions.Time.Testing;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public void When_Thirty_Days_Pass_Should_Report_The_Order_As_Expired()
    {
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new OrderService(time);
        var order = service.CreateOrder("customer-1", 99.99m);

        // Act
        time.Advance(TimeSpan.FromDays(31));

        // Assert
        Assert.True(service.IsOrderExpired(order));
    }
}
```

### Code not moved yet (`IDateTimeProvider`)

```csharp
#pragma warning disable TAS0001
var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
IDateTimeProvider legacyClock = new DateTimeProvider(time);   // the same clock as the new code

Assert.Equal(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc), legacyClock.UtcNow);
#pragma warning restore TAS0001
```

### Equivalents for the migration

| `IDateTimeProvider` | `TimeProvider` |
|---------------------|----------------|
| `UtcNow` | `GetUtcNow().UtcDateTime` (or `GetUtcNow()` as a `DateTimeOffset`) |
| `Now` | `GetLocalNow().DateTime` |
| `UtcToday` | `GetUtcNow().UtcDateTime.Date` |
| `Today` | `GetLocalNow().Date` |
| `UtcTimeOfDay` | `GetUtcNow().TimeOfDay` |
| `TimeOfDay` | `GetLocalNow().TimeOfDay` |

---

## API Reference

| Type | Kind | Description |
|------|------|-------------|
| `IDateTimeProvider` | Interface (obsolete, `TAS0001`) | A clock replaceable in tests; superseded by `System.TimeProvider` |
| `DateTimeProvider` | Class (obsolete, `TAS0001`) | Implementation on `TimeProvider`: `new DateTimeProvider()` reads the system clock, `new DateTimeProvider(timeProvider)` the given one |
| `IDateTimeProvider.Now` | Property | Current time in the local time zone of the clock (`DateTimeKind.Local`) |
| `IDateTimeProvider.UtcNow` | Property | Current UTC time (`DateTimeKind.Utc`) |
| `IDateTimeProvider.Today` | Property | Current date in the local time zone of the clock |
| `IDateTimeProvider.UtcToday` | Property | Current UTC date |
| `IDateTimeProvider.TimeOfDay` | Property | Time of day (local) as a `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDay` | Property | Time of day in UTC as a `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDaty` | Property (obsolete) | Misspelled name of `UtcTimeOfDay`, kept for source compatibility |
| `DateTimeProvider.ObsoleteDiagnosticId` | Constant | `"TAS0001"` - the warning id to silence in `NoWarn` |

**Changes in 2.0:** `IDateTimeProvider` requires an implementation of `UtcTimeOfDay` (your own implementations of the interface need it added);
`Now` and `Today` report the time zone of the given `TimeProvider`, not always the system zone.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.DateTime - instructions for an AI agent

The library is obsolete since 2.0 (diagnostic TAS0001). The application clock is System.TimeProvider.

### Registration
// Program.cs
builder.Services.AddSingleton(TimeProvider.System);
// only if the project still uses IDateTimeProvider:
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

### What gets configured
- TimeProvider - the clock for new code
- IDateTimeProvider / DateTimeProvider - an adapter over the same TimeProvider for code not moved yet

### Configuration
- No configuration keys. The TAS0001 warning can be silenced during the migration: <NoWarn>$(NoWarn);TAS0001</NoWarn>

### Rules
- Inject TimeProvider in new code; do not add new uses of IDateTimeProvider
- Do not call DateTime.Now or DateTime.UtcNow directly in production code
- Use GetUtcNow() for values stored in a database
- In tests use FakeTimeProvider (package Microsoft.Extensions.TimeProvider.Testing), not a mock of the interface
- Migrate by the equivalents table: UtcNow -> GetUtcNow().UtcDateTime, Now -> GetLocalNow().DateTime
- Use UtcTimeOfDay, not the misspelled UtcTimeOfDaty
```
