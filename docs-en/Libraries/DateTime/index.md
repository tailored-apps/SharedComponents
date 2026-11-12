# TailoredApps.Shared.DateTime

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.DateTime)](https://www.nuget.org/packages/TailoredApps.Shared.DateTime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

Calling `DateTime.Now` directly in production code makes deterministic tests impossible. The library provides
`IDateTimeProvider` - the current date and time as ready-to-use values (`DateTime`, `DateTimeOffset`, `TimeSpan`), read from a clock
that can be replaced in tests.

Since 2.0 that clock is [`System.TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider) from .NET:
`DateTimeProvider` reads every value from it. An application therefore has **one clock** - code that injects `IDateTimeProvider`
and code that injects `TimeProvider` (timers, delays, .NET libraries) see the same time and the same time zone,
in tests with `FakeTimeProvider` as well.

Which one to use:

| You need | Inject |
|----------|--------|
| The current date or time as a value (`UtcNow`, `OffsetUtcNow`, `Today`, …) | `IDateTimeProvider` |
| A timer, a delay, a time measurement (`CreateTimer`, `Task.Delay(…, timeProvider)`, `GetTimestamp`) | `TimeProvider` |

---

## Installation

```bash
dotnet add package TailoredApps.Shared.DateTime
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.DateTime;

builder.Services.AddDateTimeProvider();
```

`AddDateTimeProvider()` registers, as singletons, `TimeProvider` (`TimeProvider.System`) and an `IDateTimeProvider` that reads it.
Registrations already in the container are kept - your own `TimeProvider` can be registered before or after the call,
and `IDateTimeProvider` always reads the one the container resolves.

---

## Usage example

### Production code

```csharp
public class OrderService
{
    private readonly IDateTimeProvider _dateTime;

    public OrderService(IDateTimeProvider dateTime) => _dateTime = dateTime;

    public Order CreateOrder(string customerId, decimal amount)
    {
        var now = _dateTime.OffsetUtcNow;   // DateTimeOffset: the instant together with its offset from UTC
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            CreatedAt = now,
            ExpiresAt = now.AddDays(30),
        };
    }

    public bool IsOrderExpired(Order order) => order.ExpiresAt < _dateTime.OffsetUtcNow;
}
```

### Unit test (`FakeTimeProvider`)

```bash
dotnet add package Microsoft.Extensions.TimeProvider.Testing
```

```csharp
using Microsoft.Extensions.Time.Testing;
using TailoredApps.Shared.DateTime;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public void When_Thirty_Days_Pass_Should_Report_The_Order_As_Expired()
    {
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new OrderService(new DateTimeProvider(clock));
        var order = service.CreateOrder("customer-1", 99.99m);

        // Act
        clock.Advance(TimeSpan.FromDays(31));

        // Assert
        Assert.True(service.IsOrderExpired(order));
    }
}
```

A mock of the interface (`new Mock<IDateTimeProvider>()`) works as before; `FakeTimeProvider` has the advantage that time can be
advanced and every property stays consistent with the others.

### Integration test - replacing the clock in the host

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
// IDateTimeProvider and TimeProvider in the application now both point at "clock"
```

### `DateTime` or `DateTimeOffset`

- For values that are stored in a database or sent on, use `OffsetUtcNow` (or `OffsetNow`): a `DateTimeOffset` carries the offset
  from UTC, which a `DateTime` does not - its `Kind` is lost in serialization and in most databases.
- `Now` and `Today` report the time zone of the clock (`TimeProvider.LocalTimeZone`), with `Kind == Local`.

---

## API Reference

| Type | Kind | Description |
|------|------|-------------|
| `IDateTimeProvider` | Interface | The current date and time as ready-to-use values |
| `DateTimeProvider` | Class | Implementation on `TimeProvider`: `new DateTimeProvider()` reads the system clock, `new DateTimeProvider(timeProvider)` the given one |
| `DateTimeProviderExtensions.AddDateTimeProvider()` | Extension method | Registers `TimeProvider` and `IDateTimeProvider` as singletons (without replacing existing ones) |
| `IDateTimeProvider.OffsetUtcNow` | Property | The current instant in UTC as a `DateTimeOffset` (zero offset) |
| `IDateTimeProvider.OffsetNow` | Property | The current instant in the local time zone of the clock as a `DateTimeOffset`, with that zone's offset |
| `IDateTimeProvider.UtcNow` | Property | Current UTC time as a `DateTime` (`Kind == Utc`) |
| `IDateTimeProvider.Now` | Property | Current time in the local time zone of the clock as a `DateTime` (`Kind == Local`) |
| `IDateTimeProvider.UtcToday` | Property | Current UTC date |
| `IDateTimeProvider.Today` | Property | Current date in the local time zone of the clock |
| `IDateTimeProvider.UtcTimeOfDay` | Property | Time of day in UTC as a `TimeSpan` |
| `IDateTimeProvider.TimeOfDay` | Property | Time of day (local) as a `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDaty` | Property (obsolete) | Misspelled name of `UtcTimeOfDay`; it works, and the compiler points at the correct one |
| `DateTimeProvider.TimeProvider` | Property | The clock every value is read from |

**Compatibility with 1.x:** `OffsetNow`, `OffsetUtcNow` and `UtcTimeOfDay` have default implementations in the interface, so your own
implementations of `IDateTimeProvider` and existing mocks compile unchanged. Behaviour change: `Now` and `Today` report the time zone
of the `TimeProvider` in use rather than always the system zone (the same thing for the system clock).

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.DateTime - instructions for an AI agent

You are using TailoredApps.Shared.DateTime: IDateTimeProvider is the current date and time read from System.TimeProvider.

### Registration
// Program.cs
builder.Services.AddDateTimeProvider();

### What gets configured
- TimeProvider (TimeProvider.System) - the only clock of the application, singleton
- IDateTimeProvider (DateTimeProvider) - date and time values read from that clock, singleton
- Existing registrations of TimeProvider and IDateTimeProvider are not replaced

### Configuration
- No configuration keys. Another clock: services.AddSingleton<TimeProvider>(myClock) before or after AddDateTimeProvider()

### Rules
- Do not call DateTime.Now, DateTime.UtcNow or DateTimeOffset.UtcNow directly in production code
- Inject IDateTimeProvider when a date or time value is needed; TimeProvider when a timer, a delay or a time measurement is needed
- For values stored in a database or sent on use OffsetUtcNow (DateTimeOffset), not UtcNow (DateTime)
- Now / Today / OffsetNow only where local time is needed (for display, for example)
- Use UtcTimeOfDay, not the misspelled UtcTimeOfDaty
- In tests: new DateTimeProvider(new FakeTimeProvider(...)) from the Microsoft.Extensions.TimeProvider.Testing package; move time with Advance
- Do not register a second, independent clock next to TimeProvider - the application must have one source of time
```
