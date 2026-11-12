# TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

ASP.NET Core Web API integration for Unit of Work — eliminates the boilerplate of manually committing and rolling back transactions in every controller. The library provides `TransactionFilterAttribute` — a global ASP.NET Core action filter that automatically:

- **Sets the isolation level** before the controller action executes, when the action carries `[TransactionIsolationLevel]`
- **Commits** on successful completion (pending changes are saved first)
- **Rolls back** on exception or when the action returned a 4xx/5xx result

The filter does not open the transaction itself. The unit of work opens it lazily: on the first `SaveChanges` / `SaveChangesAsync` (the commit at the end of the action counts) or when you call `BeginTransactionManually()`. Queries executed before that moment run outside the transaction.

Optionally decorate an action with `[TransactionIsolationLevel(IsolationLevel.Serializable)]` to set a specific isolation level for that endpoint. The attribute is read from the action method only - placed on a controller class it has no effect.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore;

// 1. Register UoW + TransactionFilterAttribute
builder.Services
    .AddUnitOfWorkForWebApi<IApplicationDbContext, ApplicationDbContext>();

// 2. Register it as a global filter
builder.Services.AddControllers(options =>
{
    options.Filters.AddUnitOfWorkTransactionAttribute();
});
```

---

## Usage example

### Controller — transaction managed automatically

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
        // You do not have to commit manually — TransactionFilter does it when the action completes
        var order = new Order { CustomerId = dto.CustomerId, Amount = dto.Amount };
        _uow.DataProvider.Orders.Add(order);
        await _uow.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    // Endpoint with a higher isolation level — prevents phantom reads
    [HttpPost("transfer")]
    [TransactionIsolationLevel(System.Data.IsolationLevel.Serializable)]
    public async Task<IActionResult> TransferFunds([FromBody] TransferDto dto)
    {
        // Open the transaction now, so that the reads below run inside it
        // (otherwise it would open only on the first SaveChanges)
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

If the action throws an exception, `TransactionFilterAttribute` automatically calls `RollbackTransaction()`. Otherwise it calls `CommitTransaction()`, which saves pending changes before committing - an action that only adds or changes entities does not have to call `SaveChanges` itself.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `TransactionFilterAttribute` | Action Filter | Automatic transaction commit/rollback for every action |
| `TransactionIsolationLevelAttribute` | Attribute | Sets the isolation level for a specific action; the filter reads it from the action method only |
| `UnitOfWorkConfiguration.AddUnitOfWorkForWebApi<TI, T>` | Ext. method | Registers UoW + filter in DI |
| `UnitOfWorkConfiguration.AddUnitOfWorkTransactionAttribute` | Ext. method | Adds `TransactionFilterAttribute` as a global filter |

---

## 🔒 Transaction consistency

- **Rollback on error responses.** The filter rolls back not only after an exception but also when the action returned a 4xx/5xx result (`BadRequest`, `Conflict`, `StatusCode(500)`). Partial writes made before `return Conflict(...)` are no longer persisted.
- **Commit failures stay visible.** When both commit and rollback fail, `ActionExecutedContext.Exception` receives an `AggregateException` with both (previously the rollback exception replaced the root cause and escaped the filter).
- `UnitOfWork` disposes the transaction in `finally`, so a failed commit never leaves an open transaction on the connection.
- **The transaction opens lazily.** It starts on the first `SaveChanges` / `SaveChangesAsync` or on `BeginTransactionManually()`, not when the action starts. A read that must be protected by the transaction (check-then-write, for example) needs `BeginTransactionManually()` before it.
- **Supported isolation levels.** The default is `ReadCommitted`. The unit of work supports `ReadCommitted`, `ReadUncommitted` and `Serializable`; any other level throws `ArgumentOutOfRangeException` when the database connection is opened.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore - instructions for an AI agent

You are using automatic transaction management through TransactionFilterAttribute in ASP.NET Core.

### Registration
```csharp
// Program.cs
builder.Services.AddUnitOfWorkForWebApi<IMyDbContext, MyDbContext>();
builder.Services.AddControllers(o => o.Filters.AddUnitOfWorkTransactionAttribute());
```

### Behaviour
- The filter ends every controller action with a commit or a rollback of the unit of work
- Success → CommitTransaction() (saves pending changes first)
- Exception or a 4xx/5xx result → RollbackTransaction()
- The transaction is opened on the first SaveChanges/SaveChangesAsync or by BeginTransactionManually(), not at the start of the action

### Isolation level per action
```csharp
[TransactionIsolationLevel(IsolationLevel.Serializable)]
public async Task<IActionResult> CriticalOperation() { ... }
```

### Rules
- Do not call CommitTransaction/RollbackTransaction manually in controllers — the filter does it
- Put TransactionIsolationLevelAttribute on the action method - the filter does not read it from the controller class
- The default isolation level is ReadCommitted; only ReadCommitted, ReadUncommitted and Serializable are supported
- Call BeginTransactionManually() before reads that have to run inside the transaction
- Return 4xx/5xx codes for business failures - the filter treats them like an exception and rolls back
````
