# TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Integracja Unit of Work z ASP.NET Core Web API — eliminuje boilerplate polegający na ręcznym commicie i rollbacku transakcji w każdym kontrolerze. Biblioteka dostarcza `TransactionFilterAttribute` — globalny ASP.NET Core action filter, który automatycznie:

- **Otwiera transakcję** przed wykonaniem akcji kontrolera
- **Commituje** po pomyślnym wykonaniu
- **Rollbackuje** w przypadku wyjątku

Opcjonalnie możesz udekorować akcję atrybutem `[TransactionIsolationLevel(IsolationLevel.Serializable)]`, aby ustawić konkretny poziom izolacji dla danego endpointu.

## 🇬🇧 Description

ASP.NET Core Web API integration for Unit of Work — eliminates the boilerplate of manually committing and rolling back transactions in every controller. The library provides `TransactionFilterAttribute` — a global ASP.NET Core action filter that automatically:

- **Opens a transaction** before the controller action executes
- **Commits** on successful completion
- **Rolls back** on exception

Optionally decorate an action with `[TransactionIsolationLevel(IsolationLevel.Serializable)]` to set a specific isolation level for that endpoint.

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
        var from = await _uow.DataProvider.Accounts.FindAsync(dto.FromId);
        var to = await _uow.DataProvider.Accounts.FindAsync(dto.ToId);

        from.Balance -= dto.Amount;
        to.Balance += dto.Amount;

        await _uow.SaveChangesAsync();
        return Ok();
    }
}
```

Jeśli akcja rzuci wyjątek, `TransactionFilterAttribute` automatycznie wywoła `RollbackTransaction()`.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `TransactionFilterAttribute` | Action Filter | Automatyczne commit/rollback transakcji dla każdej akcji |
| `TransactionIsolationLevelAttribute` | Atrybut | Ustawia poziom izolacji dla konkretnej akcji lub kontrolera |
| `UnitOfWorkConfiguration.AddUnitOfWorkForWebApi<TI, T>` | Metoda ext. | Rejestruje UoW + filter w DI |
| `UnitOfWorkConfiguration.AddUnitOfWorkTransactionAttribute` | Metoda ext. | Dodaje `TransactionFilterAttribute` jako globalny filter |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore — Instrukcja dla agenta AI

Używasz automatycznego zarządzania transakcjami przez TransactionFilterAttribute w ASP.NET Core.

### Rejestracja
```csharp
// Program.cs
builder.Services.AddUnitOfWorkForWebApi<IMyDbContext, MyDbContext>();
builder.Services.AddControllers(o => o.Filters.AddUnitOfWorkTransactionAttribute());
```

### Zachowanie
- Każda akcja kontrolera jest automatycznie opakowana w transakcję
- Sukces → CommitTransaction()
- Wyjątek → RollbackTransaction()

### Poziom izolacji per akcja
```csharp
[TransactionIsolationLevel(IsolationLevel.Serializable)]
public async Task<IActionResult> CriticalOperation() { ... }
```

### Zasady
- Nie wywołuj ręcznie CommitTransaction/RollbackTransaction w kontrolerach — filter to robi
- TransactionIsolationLevelAttribute można stosować na klasie kontrolera lub na metodzie
- Domyślny poziom izolacji pochodzi z konfiguracji UoW
```
