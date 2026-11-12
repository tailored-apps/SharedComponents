# TailoredApps.Shared.DateTime

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.DateTime)](https://www.nuget.org/packages/TailoredApps.Shared.DateTime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Bezpośrednie użycie `DateTime.Now` w kodzie produkcyjnym uniemożliwia pisanie deterministycznych testów. Biblioteka dostarcza
`IDateTimeProvider` — bieżącą datę i czas jako gotowe wartości (`DateTime`, `DateTimeOffset`, `TimeSpan`), odczytywane z zegara,
który można podmienić w testach.

Od 2.0 zegarem jest [`System.TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider) z .NET:
`DateTimeProvider` czyta z niego każdą wartość. W aplikacji jest więc **jeden zegar** — kod wstrzykujący `IDateTimeProvider`
i kod wstrzykujący `TimeProvider` (timery, opóźnienia, biblioteki .NET) widzą ten sam czas i tę samą strefę czasową,
także w testach z `FakeTimeProvider`.

Kiedy czego użyć:

| Potrzebujesz | Wstrzyknij |
|--------------|------------|
| Bieżącej daty lub czasu jako wartości (`UtcNow`, `OffsetUtcNow`, `Today`, …) | `IDateTimeProvider` |
| Timera, opóźnienia, pomiaru czasu (`CreateTimer`, `Task.Delay(…, timeProvider)`, `GetTimestamp`) | `TimeProvider` |

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.DateTime
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.DateTime;

builder.Services.AddDateTimeProvider();
```

`AddDateTimeProvider()` rejestruje jako singletony `TimeProvider` (`TimeProvider.System`) oraz `IDateTimeProvider`, który z niego czyta.
Rejestracje już obecne w kontenerze zostają — własny `TimeProvider` można zarejestrować przed wywołaniem albo po nim,
a `IDateTimeProvider` zawsze czyta ten, który rozwiązuje kontener.

---

## Przykład użycia

### Kod produkcyjny

```csharp
public class OrderService
{
    private readonly IDateTimeProvider _dateTime;

    public OrderService(IDateTimeProvider dateTime) => _dateTime = dateTime;

    public Order CreateOrder(string customerId, decimal amount)
    {
        var now = _dateTime.OffsetUtcNow;   // DateTimeOffset: chwila razem z przesunięciem względem UTC
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

### Test jednostkowy (`FakeTimeProvider`)

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

Mock interfejsu (`new Mock<IDateTimeProvider>()`) działa jak dotąd; `FakeTimeProvider` ma tę przewagę, że czas można przesuwać,
a wszystkie właściwości pozostają ze sobą spójne.

### Test integracyjny — podmiana zegara w hoście

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
// IDateTimeProvider i TimeProvider w aplikacji wskazują teraz na "clock"
```

### `DateTime` czy `DateTimeOffset`

- Dla wartości zapisywanych w bazie lub wysyłanych dalej używaj `OffsetUtcNow` (albo `OffsetNow`): `DateTimeOffset` przenosi
  przesunięcie względem UTC, którego `DateTime` nie ma — jego `Kind` gubi się przy serializacji i w większości baz.
- `Now` i `Today` raportują strefę czasową zegara (`TimeProvider.LocalTimeZone`), z `Kind == Local`.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IDateTimeProvider` | Interfejs | Bieżąca data i czas jako gotowe wartości |
| `DateTimeProvider` | Klasa | Implementacja na `TimeProvider`: `new DateTimeProvider()` czyta zegar systemowy, `new DateTimeProvider(timeProvider)` — podany |
| `DateTimeProviderExtensions.AddDateTimeProvider()` | Metoda rozszerzająca | Rejestruje `TimeProvider` i `IDateTimeProvider` jako singletony (bez nadpisywania istniejących) |
| `IDateTimeProvider.OffsetUtcNow` | Właściwość | Aktualna chwila w UTC jako `DateTimeOffset` (przesunięcie zero) |
| `IDateTimeProvider.OffsetNow` | Właściwość | Aktualna chwila w lokalnej strefie zegara jako `DateTimeOffset`, z przesunięciem tej strefy |
| `IDateTimeProvider.UtcNow` | Właściwość | Aktualny czas UTC jako `DateTime` (`Kind == Utc`) |
| `IDateTimeProvider.Now` | Właściwość | Aktualny czas w lokalnej strefie zegara jako `DateTime` (`Kind == Local`) |
| `IDateTimeProvider.UtcToday` | Właściwość | Aktualna data UTC |
| `IDateTimeProvider.Today` | Właściwość | Aktualna data w lokalnej strefie zegara |
| `IDateTimeProvider.UtcTimeOfDay` | Właściwość | Pora dnia UTC jako `TimeSpan` |
| `IDateTimeProvider.TimeOfDay` | Właściwość | Pora dnia (lokalnie) jako `TimeSpan` |
| `DateTimeProvider.TimeProvider` | Właściwość | Zegar, z którego czytane są wszystkie wartości |

**Zgodność z 1.x:** `OffsetNow`, `OffsetUtcNow` i `UtcTimeOfDay` mają w interfejsie implementacje domyślne, więc własne implementacje
`IDateTimeProvider` i istniejące mocki kompilują się bez zmian. Zmiana zachowania: `Now` i `Today` raportują strefę czasową
użytego `TimeProvider`, a nie zawsze strefę systemu (dla zegara systemowego to to samo).

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.DateTime — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.DateTime: IDateTimeProvider to bieżąca data i czas odczytywane z System.TimeProvider.

### Rejestracja
// Program.cs
builder.Services.AddDateTimeProvider();

### Co jest konfigurowane
- TimeProvider (TimeProvider.System) — jedyny zegar aplikacji, singleton
- IDateTimeProvider (DateTimeProvider) — wartości daty i czasu odczytywane z tego zegara, singleton
- Istniejące rejestracje TimeProvider i IDateTimeProvider nie są nadpisywane

### Konfiguracja
- Brak kluczy konfiguracji. Inny zegar: services.AddSingleton<TimeProvider>(mojZegar) przed lub po AddDateTimeProvider()

### Zasady
- Nie używaj DateTime.Now, DateTime.UtcNow ani DateTimeOffset.UtcNow bezpośrednio w kodzie produkcyjnym
- Wstrzykuj IDateTimeProvider, gdy potrzebna jest wartość daty lub czasu; TimeProvider, gdy potrzebny jest timer, opóźnienie lub pomiar czasu
- Dla wartości zapisywanych w bazie lub wysyłanych dalej używaj OffsetUtcNow (DateTimeOffset), nie UtcNow (DateTime)
- Now / Today / OffsetNow tylko tam, gdzie potrzebny jest czas lokalny (np. do wyświetlania)
- Używaj UtcTimeOfDay; błędnie zapisane UtcTimeOfDaty usunięto w 3.0
- W testach: new DateTimeProvider(new FakeTimeProvider(...)) z pakietu Microsoft.Extensions.TimeProvider.Testing; przesuwaj czas przez Advance
- Nie rejestruj drugiego, niezależnego zegara obok TimeProvider — aplikacja ma mieć jedno źródło czasu
```
