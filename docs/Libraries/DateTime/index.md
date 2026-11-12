# TailoredApps.Shared.DateTime

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.DateTime)](https://www.nuget.org/packages/TailoredApps.Shared.DateTime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

!!! warning "Przestarzałe od 2.0 — używaj `System.TimeProvider`"
    .NET ma własną abstrakcję zegara: [`System.TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider).
    `IDateTimeProvider` i `DateTimeProvider` są oznaczone `[Obsolete]` (diagnostyka **`TAS0001`**, ostrzeżenie, nie błąd)
    i zostaną usunięte w 3.0. W nowym kodzie wstrzykuj `TimeProvider`.

Biblioteka dostarcza interfejs `IDateTimeProvider` i implementację `DateTimeProvider` — zegar, który można podmienić w testach,
zamiast wywoływać `DateTime.Now` wprost.

Od 2.0 `DateTimeProvider` odczytuje czas z `TimeProvider`. Dzięki temu migracja może być stopniowa: rejestrujesz jeden
`TimeProvider`, a kod jeszcze używający `IDateTimeProvider` i kod już przeniesiony na `TimeProvider` widzą **ten sam zegar**
i tę samą strefę czasową — także w testach z `FakeTimeProvider`.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.DateTime
```

W nowych projektach pakiet nie jest potrzebny — `TimeProvider` jest częścią .NET.

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.DateTime;

// Docelowo: tylko zegar z .NET.
builder.Services.AddSingleton(TimeProvider.System);

// Na czas migracji: IDateTimeProvider czytający ten sam zegar.
#pragma warning disable TAS0001
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
#pragma warning restore TAS0001
```

Kontener wybiera konstruktor `DateTimeProvider(TimeProvider)`, gdy `TimeProvider` jest zarejestrowany; w przeciwnym razie
bezparametrowy, czytający zegar systemowy.

---

## Przykład użycia

### Kod docelowy (`TimeProvider`)

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

### Test jednostkowy (`FakeTimeProvider`)

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

### Kod jeszcze nieprzeniesiony (`IDateTimeProvider`)

```csharp
#pragma warning disable TAS0001
var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
IDateTimeProvider legacyClock = new DateTimeProvider(time);   // ten sam zegar co w nowym kodzie

Assert.Equal(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc), legacyClock.UtcNow);
#pragma warning restore TAS0001
```

### Odpowiedniki przy migracji

| `IDateTimeProvider` | `TimeProvider` |
|---------------------|----------------|
| `UtcNow` | `GetUtcNow().UtcDateTime` (lub `GetUtcNow()` jako `DateTimeOffset`) |
| `Now` | `GetLocalNow().DateTime` |
| `UtcToday` | `GetUtcNow().UtcDateTime.Date` |
| `Today` | `GetLocalNow().Date` |
| `UtcTimeOfDay` | `GetUtcNow().TimeOfDay` |
| `TimeOfDay` | `GetLocalNow().TimeOfDay` |

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IDateTimeProvider` | Interfejs (przestarzały, `TAS0001`) | Zegar do podmiany w testach; zastąpiony przez `System.TimeProvider` |
| `DateTimeProvider` | Klasa (przestarzała, `TAS0001`) | Implementacja na `TimeProvider`: `new DateTimeProvider()` czyta zegar systemowy, `new DateTimeProvider(timeProvider)` — podany |
| `IDateTimeProvider.Now` | Właściwość | Aktualny czas w lokalnej strefie zegara (`DateTimeKind.Local`) |
| `IDateTimeProvider.UtcNow` | Właściwość | Aktualny czas UTC (`DateTimeKind.Utc`) |
| `IDateTimeProvider.Today` | Właściwość | Aktualna data w lokalnej strefie zegara |
| `IDateTimeProvider.UtcToday` | Właściwość | Aktualna data UTC |
| `IDateTimeProvider.TimeOfDay` | Właściwość | Pora dnia (lokalnie) jako `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDay` | Właściwość | Pora dnia UTC jako `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDaty` | Właściwość (przestarzała) | Błędnie zapisana nazwa `UtcTimeOfDay`, zachowana dla zgodności źródłowej |
| `DateTimeProvider.ObsoleteDiagnosticId` | Stała | `"TAS0001"` — identyfikator ostrzeżenia do wyciszenia w `NoWarn` |

**Zmiany w 2.0:** `IDateTimeProvider` wymaga implementacji `UtcTimeOfDay` (własne implementacje interfejsu trzeba uzupełnić);
`Now` i `Today` raportują strefę czasową przekazanego `TimeProvider`, a nie zawsze strefę systemu.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.DateTime — Instrukcja dla agenta AI

Biblioteka jest przestarzała od 2.0 (diagnostyka TAS0001). Zegarem aplikacji jest System.TimeProvider.

### Rejestracja
// Program.cs
builder.Services.AddSingleton(TimeProvider.System);
// tylko jeśli projekt nadal używa IDateTimeProvider:
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

### Co jest konfigurowane
- TimeProvider — zegar dla nowego kodu
- IDateTimeProvider / DateTimeProvider — adapter na ten sam TimeProvider dla kodu jeszcze nieprzeniesionego

### Konfiguracja
- Brak kluczy konfiguracji. Ostrzeżenie TAS0001 można wyciszyć na czas migracji: <NoWarn>$(NoWarn);TAS0001</NoWarn>

### Zasady
- W nowym kodzie wstrzykuj TimeProvider; nie dodawaj nowych użyć IDateTimeProvider
- Nie używaj DateTime.Now ani DateTime.UtcNow bezpośrednio w kodzie produkcyjnym
- Dla wartości zapisywanych w bazie używaj GetUtcNow()
- W testach używaj FakeTimeProvider (pakiet Microsoft.Extensions.TimeProvider.Testing), nie mocka interfejsu
- Migruj według tabeli odpowiedników: UtcNow -> GetUtcNow().UtcDateTime, Now -> GetLocalNow().DateTime
- Używaj UtcTimeOfDay, nie błędnie zapisanego UtcTimeOfDaty
```
