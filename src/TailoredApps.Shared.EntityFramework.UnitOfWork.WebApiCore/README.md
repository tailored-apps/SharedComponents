# TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore

ASP.NET Core integration for the unit of work from `TailoredApps.Shared.EntityFramework`. An MVC action filter
ends every controller action by committing the unit of work, or by rolling it back when the action failed, so
controllers do not commit or roll back by hand.

## Installation

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore
```

## Registration

```csharp
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore;

// AppDbContext : DbContext, IAppDbContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddUnitOfWorkForWebApi<IAppDbContext, AppDbContext>();

builder.Services.AddControllers(options => options.Filters.AddUnitOfWorkTransactionAttribute());
```

`AddUnitOfWorkForWebApi<TInterface, TContext>()` registers `IUnitOfWork`, `IUnitOfWork<TInterface>`, the context
interface and `TransactionFilterAttribute`, all scoped. It returns `IUnitOfWorkOptionsBuilder`, on which the
save and transaction hooks of `TailoredApps.Shared.EntityFramework` are added. `AddUnitOfWorkTransactionAttribute()`
adds the filter to every controller action.

## Usage

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

[ApiController]
[Route("api/orders")]
public class OrdersController(IUnitOfWork<IAppDbContext> unitOfWork) : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateOrder command)
    {
        if (command.Lines.Count == 0)
        {
            return BadRequest();   // 4xx result: the filter rolls back
        }

        unitOfWork.DataProvider.Orders.Add(new Order { Number = command.Number });
        return Ok();               // the filter saves the pending changes and commits
    }
}
```

An action can ask for another isolation level:

```csharp
using System.Data;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes;

[HttpPost("transfer")]
[TransactionIsolationLevel(IsolationLevel.Serializable)]
public async Task<IActionResult> Transfer(TransferFunds command) { /* ... */ }
```

## Good to know

- The filter rolls back when the action threw or returned a result with a status code of 400 or higher
  (`BadRequest()`, `Conflict()`, `StatusCode(500)`). A rollback also detaches every tracked entity.
- Otherwise it calls `CommitTransaction()`, which saves pending changes first; an action does not have to call
  `SaveChanges` itself.
- The transaction is opened lazily: by the first `SaveChanges` / `SaveChangesAsync`, or by
  `BeginTransactionManually()`. Queries executed before that run outside the transaction.
- When the commit fails the filter rolls back and reports the commit exception as the action's exception
  (an `AggregateException` with both when the rollback fails too).
- `[TransactionIsolationLevel]` is read from the action method only; placed on a controller class it has no
  effect. The default level is `ReadCommitted`. The unit of work supports `ReadCommitted`, `ReadUncommitted`
  and `Serializable`; another level throws `ArgumentOutOfRangeException` when the connection is opened.
- The `DbContext` is resolved from the container, so it has to be registered (`AddDbContext`).

## Documentation

https://shared.tailoredapps.pl/en/Libraries/EntityFramework/UnitOfWork.WebApiCore/
