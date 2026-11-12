# TailoredApps.Shared.EntityFramework

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a complete implementation of the **Unit of Work** pattern on top of Entity Framework Core. It solves the problem of uncontrolled transaction management in multi-layer applications — instead of calling `SaveChanges()` directly in each repository, you have a single control point (`IUnitOfWork`) managing the transaction lifecycle.

Key capabilities:
- **Transactions** with configurable isolation levels
- **Auditing** — automatic collection of entity changes (which entity, its state, original and current values), handed to your `IEntityChangesAuditor` when the transaction is committed
- **Hooks** — `IHook` implementations that execute code before/after `SaveChanges` or after a transaction commit/rollback
- **InMemory** provider support for testing

---

## Installation

```bash
dotnet add package TailoredApps.Shared.EntityFramework
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration;

// The DbContext itself is registered the usual way (AddDbContext<ApplicationDbContext>(...))

// Basic registration
builder.Services.AddUnitOfWork<IApplicationDbContext, ApplicationDbContext>();

// ...or with hooks and auditing (see the examples below)
builder.Services.AddUnitOfWork<IApplicationDbContext, ApplicationDbContext>()
    .WithPostSaveChangesHook<SaveLoggingHook>()
    .WithUnitOfWorkAudit<ApplicationDbContext, AuditLogAuditor>(settings =>
    {
        settings.TypesToCollect = new[] { typeof(Order), typeof(Customer) };
        settings.EntityStatesToCollect = new[]
        {
            AuditEntityState.Added,
            AuditEntityState.Modified,
            AuditEntityState.Deleted
        };
    });
```

Where `IApplicationDbContext` is the interface of your DbContext and `ApplicationDbContext` is the implementation inheriting from `DbContext`. `AddUnitOfWork` resolves `ApplicationDbContext` from the container, so it has to be registered. `WithUnitOfWorkAudit` requires at least one entry in both `TypesToCollect` and `EntityStatesToCollect`; an empty list throws `InvalidAuditConfigurationException`.

---

## Usage example

### Context definition

```csharp
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Account> Accounts { get; }
}

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Account> Accounts { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }
}
```

### Usage in a service

```csharp
public class OrderService
{
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public OrderService(IUnitOfWork<IApplicationDbContext> uow)
    {
        _uow = uow;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            TotalAmount = request.TotalAmount,
            Status = OrderStatus.Pending
        };

        _uow.DataProvider.Orders.Add(order);
        await _uow.SaveChangesAsync();

        return order;
    }

    public async Task TransferFundsAsync(int fromId, int toId, decimal amount)
    {
        // Manual transaction control with an isolation level
        _uow.SetIsolationLevel(System.Data.IsolationLevel.Serializable);

        // Without this call the transaction would open only on the first SaveChanges,
        // and the reads below would run outside it
        _uow.BeginTransactionManually();

        try
        {
            var from = await _uow.DataProvider.Accounts.FindAsync(fromId);
            var to = await _uow.DataProvider.Accounts.FindAsync(toId);

            from.Balance -= amount;
            to.Balance += amount;

            await _uow.SaveChangesAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollbackTransaction();
            throw;
        }
    }
}
```

### Hook — example of logging after a save

A hook implements one of the four hook interfaces - `IPreSaveChangesHook`, `IPostSaveChangesHook`, `ITransactionCommitHook`, `ITransactionRollbackHook` - which all derive from `IHook` and have a single method, `Execute()`, without parameters. Register it with the matching `With...Hook<T>()` method on the builder returned by `AddUnitOfWork`.

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

// Runs after every SaveChanges / SaveChangesAsync of the unit of work
public class SaveLoggingHook : IPostSaveChangesHook
{
    private readonly ILogger<SaveLoggingHook> _logger;

    public SaveLoggingHook(ILogger<SaveLoggingHook> logger) => _logger = logger;

    public void Execute() => _logger.LogInformation("Unit of work saved its changes");
}
```

### Auditing — example of logging entity changes

A hook does not receive the changed entities. They are collected by the audit extension (`WithUnitOfWorkAudit`) and passed to your `IEntityChangesAuditor` after the transaction is committed; a rollback discards them.

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

public class AuditLogAuditor : IEntityChangesAuditor
{
    private readonly ILogger<AuditLogAuditor> _logger;

    public AuditLogAuditor(ILogger<AuditLogAuditor> logger) => _logger = logger;

    public void AuditChanges(IEnumerable<EntityChange> entityChanges)
    {
        foreach (var change in entityChanges)
            _logger.LogInformation("Entity {Type}: {State}",
                change.EntityType?.Name, change.State);
    }
}
```

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `IUnitOfWork` | Interface | Transaction management: `SaveChanges`, `CommitTransaction`, `RollbackTransaction` |
| `IUnitOfWork<T>` | Interface | Extends `IUnitOfWork` with `DataProvider` (access to the DbContext) |
| `IUnitOfWorkContext` | Interface | Low-level operations: `BeginTransaction`, `SaveChanges`, `DiscardChanges` |
| `UnitOfWorkContext<T>` | Class | EF Core implementation of `IUnitOfWorkContext` |
| `ITransaction` | Interface | Transaction: `Commit()`, `Rollback()`, `Dispose()` |
| `IHook` | Interface | Base contract of UoW lifecycle hooks: `Execute()` |
| `IPreSaveChangesHook`, `IPostSaveChangesHook`, `ITransactionCommitHook`, `ITransactionRollbackHook` | Interfaces | The four hook kinds (all derive from `IHook`): before / after `SaveChanges`, after commit, after rollback |
| `IUnitOfWorkOptionsBuilder` | Interface | Returned by `AddUnitOfWork`: `WithPreSaveChangesHook<T>()`, `WithPostSaveChangesHook<T>()`, `WithTransactionCommitHook<T>()`, `WithTransactionRollbackHook<T>()` |
| `IHooksManager` | Interface | Manages the collection of hooks and their execution |
| `UnitOfWorkOptionsBuilderExtensions.WithUnitOfWorkAudit<TDbContext, TAuditor>` | Ext. method | Turns on auditing with the given `IEntityChangesAuditor` |
| `IAuditSettings` | Interface | Auditing configuration: `TypesToCollect`, `EntityStatesToCollect` |
| `IEntityChangesAuditor` | Interface | Your receiver of the collected changes: `AuditChanges(IEnumerable<EntityChange>)`, called after a commit |
| `EntityChange` | Class | Description of a change: `EntityType`, `State`, `Original`, `Current`, `PrimaryKeys` |
| `AuditEntityState` | Enum | Added, Modified, Deleted |

---

## 🔒 Query security

- **Page size is bounded.** `PagingQuery<T>` rejects (`ArgumentOutOfRangeException`) `Page < 1`, `Count < 1`, `Count > PagingQuery<T>.MaxPageSize` (default 1000, settable globally or per constructor) and `Skip` overflow. A `?count=2147483647` request no longer materialises the whole table.
- **Sort fields are validated.** `ApplySorting` accepts a single property name only (`^[A-Za-z_][A-Za-z0-9_]*$`) - `Owner.PasswordHash`, `Name, Id desc` or expressions throw `ArgumentException`, and the message does not reveal type or column names. The `ApplySorting(sorting, allowedSortFields)` overload restricts sorting to an explicit list of properties - use it for entities with sensitive columns.

```csharp
var page = await db.Users
    .ApplySorting(request, allowedSortFields: new[] { "Name", "CreatedDateUtc" })
    .PagingAsync(request);
```

!!! note "PagingAsync"
    Up to 2.0.0-preview `PagingAsync` failed with `InvalidOperationException` ("The source 'IQueryable' doesn't implement
    'IAsyncEnumerable'") for every non-empty page, because it materialised the `PagingQuery<T>` wrapper instead of the EF Core query.
    Fixed: the page is read from `PagingQuery<T>.Query`. Use the same property when you call `ToListAsync` on a `PagingQuery<T>` yourself.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.EntityFramework - instructions for an AI agent

You are using the TailoredApps.Shared.EntityFramework library (Unit of Work pattern on EF Core).

### Registration
```csharp
builder.Services.AddUnitOfWork<IMyDbContext, MyDbContext>();
```

### Usage
```csharp
// Inject IUnitOfWork<IMyDbContext>
_uow.DataProvider.Orders.Add(order);
await _uow.SaveChangesAsync();

// Manual transaction
_uow.SetIsolationLevel(IsolationLevel.ReadCommitted);
try {
    // operations...
    await _uow.SaveChangesAsync();
    _uow.CommitTransaction();
} catch {
    _uow.RollbackTransaction();
    throw;
}
```

### Rules
- Never call DbContext.SaveChanges() directly — use _uow.SaveChangesAsync()
- By default the transaction is opened on the first SaveChanges and committed automatically by TransactionFilter (if you use WebApiCore)
- For tests: use the InMemory provider — UnitOfWorkContext handles it automatically
- HasOpenTransaction checks whether a transaction is open
- Reads that must run inside the transaction need BeginTransactionManually() first - otherwise it opens on the first SaveChanges
- Hooks implement IPreSaveChangesHook / IPostSaveChangesHook / ITransactionCommitHook / ITransactionRollbackHook (one method: Execute()) and are registered with With...Hook<T>() on the builder returned by AddUnitOfWork
- Entity changes are not passed to hooks: implement IEntityChangesAuditor and register it with WithUnitOfWorkAudit<TDbContext, TAuditor>(settings => ...) setting TypesToCollect and EntityStatesToCollect
- For entities with sensitive columns always pass allowedSortFields to ApplySorting
- Do not raise PagingQuery<T>.MaxPageSize without need - the limit prevents dumping a table in one request
````
