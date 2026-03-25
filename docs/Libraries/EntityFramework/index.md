# TailoredApps.Shared.EntityFramework

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Biblioteka dostarcza kompletną implementację wzorca **Unit of Work** na bazie Entity Framework Core. Rozwiązuje problem niekontrolowanego zarządzania transakcjami w aplikacjach wielowarstwowych — zamiast bezpośrednio wywoływać `SaveChanges()` w każdym repozytorium, masz jeden punkt kontroli (`IUnitOfWork`), który zarządza cyklem życia transakcji.

Kluczowe możliwości:
- **Transakcje** z konfigurowalnymi poziomami izolacji
- **Auditing** — automatyczne śledzenie zmian encji (kto i co zmienił)
- **Hooks** — `IHook` do wykonywania kodu przed/po `SaveChanges` lub commit/rollback transakcji
- Obsługa **InMemory** provider na potrzeby testów

## 🇬🇧 Description

This library provides a complete implementation of the **Unit of Work** pattern on top of Entity Framework Core. It solves the problem of uncontrolled transaction management in multi-layer applications — instead of calling `SaveChanges()` directly in each repository, you have a single control point (`IUnitOfWork`) managing the transaction lifecycle.

Key capabilities:
- **Transactions** with configurable isolation levels
- **Auditing** — automatic entity change tracking (who changed what)
- **Hooks** — `IHook` to execute code before/after `SaveChanges` or transaction commit/rollback
- **InMemory** provider support for testing

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.EntityFramework
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework;

// Podstawowa rejestracja
builder.Services.AddUnitOfWork<IApplicationDbContext, ApplicationDbContext>()
    .WithAudit<AuditContext>(options =>
    {
        options.IgnoreProperty("RowVersion");
    });
```

Gdzie `IApplicationDbContext` to interfejs Twojego DbContext, a `ApplicationDbContext` — implementacja dziedzicząca po `DbContext`.

---

## Przykład użycia

### Definicja kontekstu

```csharp
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<Customer> Customers { get; }
}

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }
}
```

### Użycie w serwisie

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
        // Ręczna kontrola transakcji z poziomem izolacji
        _uow.SetIsolationLevel(System.Data.IsolationLevel.Serializable);

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

### Hook — przykład logowania po zapisie

```csharp
public class AuditLogHook : IHook
{
    private readonly ILogger<AuditLogHook> _logger;

    public AuditLogHook(ILogger<AuditLogHook> logger) => _logger = logger;

    public Task PostSaveChangesAsync(IEnumerable<EntityChange> changes, CancellationToken ct)
    {
        foreach (var change in changes)
            _logger.LogInformation("Entity {Type} {Id}: {State}", 
                change.EntityType, change.EntityId, change.State);
        return Task.CompletedTask;
    }
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IUnitOfWork` | Interfejs | Zarządzanie transakcją: `SaveChanges`, `CommitTransaction`, `RollbackTransaction` |
| `IUnitOfWork<T>` | Interfejs | Rozszerza `IUnitOfWork` o `DataProvider` (dostęp do DbContext) |
| `IUnitOfWorkContext` | Interfejs | Niskopoziomowe operacje: `BeginTransaction`, `SaveChanges`, `DiscardChanges` |
| `UnitOfWorkContext<T>` | Klasa | Implementacja EF Core `IUnitOfWorkContext` |
| `ITransaction` | Interfejs | Transakcja: `Commit()`, `Rollback()`, `Dispose()` |
| `IHook` | Interfejs | Marker interface dla hooków cyklu życia UoW |
| `IHooksManager` | Interfejs | Zarządza kolekcją hooków i ich wykonywaniem |
| `IAuditSettings` | Interfejs | Konfiguracja audytingu (ignorowane właściwości itp.) |
| `IEntityChangesAuditor` | Interfejs | Zbiera i zapisuje zmiany encji |
| `EntityChange` | Klasa | Opis zmiany: typ encji, ID, stary/nowy stan |
| `AuditEntityState` | Enum | Added, Modified, Deleted |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.EntityFramework — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.EntityFramework (Unit of Work pattern na EF Core).

### Rejestracja
```csharp
builder.Services.AddUnitOfWork<IMyDbContext, MyDbContext>();
```

### Użycie
```csharp
// Wstrzyknij IUnitOfWork<IMyDbContext>
_uow.DataProvider.Orders.Add(order);
await _uow.SaveChangesAsync();

// Transakcja manualna
_uow.SetIsolationLevel(IsolationLevel.ReadCommitted);
try {
    // operacje...
    await _uow.SaveChangesAsync();
    _uow.CommitTransaction();
} catch {
    _uow.RollbackTransaction();
    throw;
}
```

### Zasady
- Nigdy nie wywołuj DbContext.SaveChanges() bezpośrednio — używaj _uow.SaveChangesAsync()
- Domyślnie transakcja jest otwierana przy pierwszym SaveChanges i commitowana automatycznie przez TransactionFilter (jeśli używasz WebApiCore)
- Dla testów: użyj InMemory provider — UnitOfWorkContext automatycznie to obsługuje
- HasOpenTransaction sprawdza, czy jest otwarta transakcja
```
