# TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Integracja Unit of Work z ASP.NET Core Web API — eliminuje boilerplate polegający na ręcznym commicie i rollbacku transakcji w każdym kontrolerze. Biblioteka dostarcza `TransactionFilterAttribute` — globalny ASP.NET Core action filter, który automatycznie:

- **Ustawia poziom izolacji** przed wykonaniem akcji kontrolera, gdy akcja ma atrybut `[TransactionIsolationLevel]`
- **Commituje** po pomyślnym wykonaniu (najpierw zapisuje oczekujące zmiany)
- **Rollbackuje** w przypadku wyjątku lub gdy akcja zwróciła wynik 4xx/5xx

Filtr sam nie otwiera transakcji. Unit of Work otwiera ją leniwie: przy pierwszym `SaveChanges` / `SaveChangesAsync` (liczy się także commit na końcu akcji) albo gdy wywołasz `BeginTransactionManually()`. Zapytania wykonane wcześniej działają poza transakcją.

Opcjonalnie możesz udekorować akcję atrybutem `[TransactionIsolationLevel(IsolationLevel.Serializable)]`, aby ustawić konkretny poziom izolacji dla danego endpointu. Atrybut jest odczytywany wyłącznie z metody akcji — umieszczony na klasie kontrolera nie ma żadnego efektu.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore;

// 1. Rejestracja UoW + TransactionFilterAttribute
builder.Services
    .AddUnitOfWorkForWebApi<IApplicationDbContext, ApplicationDbContext>();

// 2. Rejestracja jako globalny filter
builder.Services.AddControllers(options =>
{
    options.Filters.AddUnitOfWorkTransactionAttribute();
});
```

---

## Przykład użycia

### Kontroler — transakcja zarządzana automatycznie

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public OrdersController(IUnitOfWork<IApplicationDbContext> uow)
    {
        _uow = uow;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        // Nie musisz ręcznie commitować — TransactionFilter zrobi to po zakończeniu akcji
        var order = new Order { CustomerId = dto.CustomerId, Amount = dto.Amount };
        _uow.DataProvider.Orders.Add(order);
        await _uow.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    // Endpoint z wyższym poziomem izolacji — zapobiega phantom reads
    [HttpPost("transfer")]
    [TransactionIsolationLevel(System.Data.IsolationLevel.Serializable)]
    public async Task<IActionResult> TransferFunds([FromBody] TransferDto dto)
    {
        // Otwórz transakcję już teraz, żeby poniższe odczyty działały w jej ramach
        // (inaczej zostałaby otwarta dopiero przy pierwszym SaveChanges)
        _uow.BeginTransactionManually();

        var from = await _uow.DataProvider.Accounts.FindAsync(dto.FromId);
        var to = await _uow.DataProvider.Accounts.FindAsync(dto.ToId);

        from.Balance -= dto.Amount;
        to.Balance += dto.Amount;

        await _uow.SaveChangesAsync();
        return Ok();
    }
}
```

Jeśli akcja rzuci wyjątek, `TransactionFilterAttribute` automatycznie wywoła `RollbackTransaction()`. W przeciwnym razie wywołuje `CommitTransaction()`, które przed commitem zapisuje oczekujące zmiany — akcja, która tylko dodaje lub zmienia encje, nie musi sama wywoływać `SaveChanges`.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `TransactionFilterAttribute` | Action Filter | Automatyczne commit/rollback transakcji dla każdej akcji |
| `TransactionIsolationLevelAttribute` | Atrybut | Ustawia poziom izolacji dla konkretnej akcji; filtr odczytuje go wyłącznie z metody akcji |
| `UnitOfWorkConfiguration.AddUnitOfWorkForWebApi<TI, T>` | Metoda ext. | Rejestruje UoW + filter w DI |
| `UnitOfWorkConfiguration.AddUnitOfWorkTransactionAttribute` | Metoda ext. | Dodaje `TransactionFilterAttribute` jako globalny filter |

---

## 🔒 Spójność transakcji

- **Rollback przy odpowiedzi błędu.** Filtr wycofuje transakcję nie tylko po wyjątku, ale też gdy akcja zwróciła wynik ze statusem 4xx/5xx (`BadRequest`, `Conflict`, `StatusCode(500)`). Częściowe zapisy sprzed `return Conflict(...)` nie są już utrwalane.
- **Błąd commitu nie ginie.** Gdy commit i rollback zawiodą, do `ActionExecutedContext.Exception` trafia `AggregateException` z oboma wyjątkami (wcześniej wyjątek rollbacku zastępował pierwotną przyczynę i wypadał z filtra).
- `UnitOfWork` zwalnia transakcję w `finally`, więc nieudany commit nie zostawia otwartej transakcji na połączeniu.
- **Transakcja otwiera się leniwie.** Zaczyna się przy pierwszym `SaveChanges` / `SaveChangesAsync` albo przy `BeginTransactionManually()`, a nie na początku akcji. Odczyt, który ma być chroniony transakcją (np. „sprawdź, potem zapisz”), wymaga wcześniejszego `BeginTransactionManually()`.
- **Obsługiwane poziomy izolacji.** Domyślny to `ReadCommitted`. Unit of Work obsługuje `ReadCommitted`, `ReadUncommitted` i `Serializable`; każdy inny poziom kończy się `ArgumentOutOfRangeException` przy otwieraniu połączenia z bazą.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore — Instrukcja dla agenta AI

Używasz automatycznego zarządzania transakcjami przez TransactionFilterAttribute w ASP.NET Core.

### Rejestracja
```csharp
// Program.cs
builder.Services.AddUnitOfWorkForWebApi<IMyDbContext, MyDbContext>();
builder.Services.AddControllers(o => o.Filters.AddUnitOfWorkTransactionAttribute());
```

### Zachowanie
- Filtr kończy każdą akcję kontrolera commitem albo rollbackiem Unit of Work
- Sukces → CommitTransaction() (najpierw zapisuje oczekujące zmiany)
- Wyjątek lub wynik 4xx/5xx → RollbackTransaction()
- Transakcja jest otwierana przy pierwszym SaveChanges/SaveChangesAsync albo przez BeginTransactionManually(), a nie na początku akcji

### Poziom izolacji per akcja
```csharp
[TransactionIsolationLevel(IsolationLevel.Serializable)]
public async Task<IActionResult> CriticalOperation() { ... }
```

### Zasady
- Nie wywołuj ręcznie CommitTransaction/RollbackTransaction w kontrolerach — filter to robi
- TransactionIsolationLevelAttribute umieszczaj na metodzie akcji — filtr nie odczytuje go z klasy kontrolera
- Domyślny poziom izolacji to ReadCommitted; obsługiwane są tylko ReadCommitted, ReadUncommitted i Serializable
- Wywołaj BeginTransactionManually() przed odczytami, które mają działać w ramach transakcji
- Zwracaj kody 4xx/5xx dla niepowodzeń biznesowych — filtr traktuje je jak wyjątek i wycofuje transakcję
````
