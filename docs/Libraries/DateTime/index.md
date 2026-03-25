# TailoredApps.Shared.DateTime

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.DateTime)](https://www.nuget.org/packages/TailoredApps.Shared.DateTime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Biblioteka rozwiązuje jeden z fundamentalnych problemów testowalności aplikacji .NET — bezpośrednie użycie `System.DateTime.Now` w kodzie produkcyjnym, które uniemożliwia pisanie deterministycznych testów jednostkowych.

`TailoredApps.Shared.DateTime` dostarcza interfejs `IDateTimeProvider` i jego domyślną implementację `DateTimeProvider`. Zamiast wywoływać `DateTime.Now` wprost, wstrzykujesz `IDateTimeProvider` przez DI i wywołujesz `provider.Now`. W testach wymieniasz implementację na mock zwracający dowolny punkt w czasie — dzięki temu testy są powtarzalne i niezależne od zegara systemowego.

## 🇬🇧 Description

This library solves one of the fundamental testability problems in .NET — direct use of `System.DateTime.Now` in production code, which prevents writing deterministic unit tests.

`TailoredApps.Shared.DateTime` provides the `IDateTimeProvider` interface and its default implementation `DateTimeProvider`. Instead of calling `DateTime.Now` directly, you inject `IDateTimeProvider` via DI and call `provider.Now`. In tests you swap the implementation for a mock that returns any point in time — making tests repeatable and independent of the system clock.

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

builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
```

---

## Przykład użycia

### Kod produkcyjny

```csharp
public class OrderService
{
    private readonly IDateTimeProvider _dateTime;

    public OrderService(IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
    }

    public Order CreateOrder(string customerId, decimal amount)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            CreatedAt = _dateTime.UtcNow,   // zamiast DateTime.UtcNow
            ExpiresAt = _dateTime.UtcNow.AddDays(30)
        };
    }

    public bool IsOrderExpired(Order order)
    {
        return order.ExpiresAt < _dateTime.UtcNow;
    }
}
```

### Test jednostkowy (Moq)

```csharp
using Moq;
using TailoredApps.Shared.DateTime;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public void CreateOrder_ShouldSetCreatedAtToCurrentUtcTime()
    {
        // Arrange
        var fixedTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeMock = new Mock<IDateTimeProvider>();
        dateTimeMock.Setup(d => d.UtcNow).Returns(fixedTime);

        var service = new OrderService(dateTimeMock.Object);

        // Act
        var order = service.CreateOrder("customer-1", 99.99m);

        // Assert
        Assert.Equal(fixedTime, order.CreatedAt);
        Assert.Equal(fixedTime.AddDays(30), order.ExpiresAt);
    }

    [Fact]
    public void IsOrderExpired_WhenExpiresInPast_ReturnsTrue()
    {
        // Arrange
        var now = new DateTime(2024, 6, 1, DateTimeKind.Utc);
        var dateTimeMock = new Mock<IDateTimeProvider>();
        dateTimeMock.Setup(d => d.UtcNow).Returns(now);

        var service = new OrderService(dateTimeMock.Object);
        var expiredOrder = new Order { ExpiresAt = now.AddDays(-1) };

        // Act & Assert
        Assert.True(service.IsOrderExpired(expiredOrder));
    }
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IDateTimeProvider` | Interfejs | Główny kontrakt — wszystkie właściwości do pobierania czasu |
| `DateTimeProvider` | Klasa | Implementacja produkcyjna — deleguje do `System.DateTime` |
| `IDateTimeProvider.Now` | Właściwość | Aktualny czas lokalny (`DateTime.Now`) |
| `IDateTimeProvider.UtcNow` | Właściwość | Aktualny czas UTC (`DateTime.UtcNow`) |
| `IDateTimeProvider.Today` | Właściwość | Aktualna data lokalna (`DateTime.Today`) |
| `IDateTimeProvider.UtcToday` | Właściwość | Aktualna data UTC (`DateTime.UtcNow.Date`) |
| `IDateTimeProvider.TimeOfDay` | Właściwość | Pora dnia (lokalnie) jako `TimeSpan` |
| `IDateTimeProvider.UtcTimeOfDaty` | Właściwość | Pora dnia UTC jako `TimeSpan` |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.DateTime — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.DateTime w projekcie .NET.

### Rejestracja
```csharp
// Program.cs
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
```

### Użycie
- Nigdy nie używaj `DateTime.Now` ani `DateTime.UtcNow` bezpośrednio w kodzie produkcyjnym
- Wstrzykuj `IDateTimeProvider` przez konstruktor
- Używaj `provider.UtcNow` dla timestampów w bazie danych
- Używaj `provider.Now` tylko gdy potrzebujesz czasu lokalnego (np. do wyświetlania)

```csharp
// ✅ Poprawnie
public class MyService
{
    private readonly IDateTimeProvider _dateTime;
    public MyService(IDateTimeProvider dateTime) => _dateTime = dateTime;
    public DateTime GetExpiry() => _dateTime.UtcNow.AddHours(1);
}

// ❌ Niepoprawnie
public class MyService
{
    public DateTime GetExpiry() => DateTime.UtcNow.AddHours(1); // nie testowalny!
}
```

### Zasady
- Zawsze używaj `IDateTimeProvider` zamiast `System.DateTime` bezpośrednio
- W testach mockuj interfejs, aby zwracał stały punkt w czasie
- Preferuj `UtcNow`/`UtcToday` dla wartości zapisywanych w bazie danych
```
