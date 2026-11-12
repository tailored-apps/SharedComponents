# TailoredApps.Shared.EntityFramework

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Biblioteka dostarcza kompletną implementację wzorca **Unit of Work** na bazie Entity Framework Core. Rozwiązuje problem niekontrolowanego zarządzania transakcjami w aplikacjach wielowarstwowych — zamiast bezpośrednio wywoływać `SaveChanges()` w każdym repozytorium, masz jeden punkt kontroli (`IUnitOfWork`), który zarządza cyklem życia transakcji.

Kluczowe możliwości:
- **Transakcje** z konfigurowalnymi poziomami izolacji
- **Auditing** — automatyczne zbieranie zmian encji (która encja, jej stan, wartości pierwotne i bieżące), przekazywanych do Twojego `IEntityChangesAuditor` po zatwierdzeniu transakcji
- **Hooks** — implementacje `IHook` wykonujące kod przed/po `SaveChanges` albo po commicie/rollbacku transakcji
- Obsługa **InMemory** provider na potrzeby testów

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.EntityFramework
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration;

// Sam DbContext rejestrujesz standardowo (AddDbContext<ApplicationDbContext>(...))

// Podstawowa rejestracja
builder.Services.AddUnitOfWork<IApplicationDbContext, ApplicationDbContext>();

// ...albo z hookami i audytem (patrz przykłady niżej)
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

Gdzie `IApplicationDbContext` to interfejs Twojego DbContext, a `ApplicationDbContext` — implementacja dziedzicząca po `DbContext`. `AddUnitOfWork` pobiera `ApplicationDbContext` z kontenera, więc musi on być zarejestrowany. `WithUnitOfWorkAudit` wymaga co najmniej jednego wpisu w `TypesToCollect` i w `EntityStatesToCollect`; pusta lista kończy się `InvalidAuditConfigurationException`.

---

## Przykład użycia

### Definicja kontekstu

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

        // Bez tego wywołania transakcja zostałaby otwarta dopiero przy pierwszym SaveChanges,
        // a poniższe odczyty działałyby poza nią
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

### Hook — przykład logowania po zapisie

Hook implementuje jeden z czterech interfejsów hooków — `IPreSaveChangesHook`, `IPostSaveChangesHook`, `ITransactionCommitHook`, `ITransactionRollbackHook` — które wszystkie dziedziczą po `IHook` i mają jedną metodę `Execute()` bez parametrów. Rejestrujesz go odpowiednią metodą `With...Hook<T>()` na builderze zwracanym przez `AddUnitOfWork`.

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;

// Wykonywany po każdym SaveChanges / SaveChangesAsync Unit of Work
public class SaveLoggingHook : IPostSaveChangesHook
{
    private readonly ILogger<SaveLoggingHook> _logger;

    public SaveLoggingHook(ILogger<SaveLoggingHook> logger) => _logger = logger;

    public void Execute() => _logger.LogInformation("Unit of work saved its changes");
}
```

### Auditing — przykład logowania zmian encji

Hook nie dostaje zmienionych encji. Zbiera je rozszerzenie audytu (`WithUnitOfWorkAudit`) i przekazuje do Twojego `IEntityChangesAuditor` po zatwierdzeniu transakcji; rollback je odrzuca.

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

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IUnitOfWork` | Interfejs | Zarządzanie transakcją: `SaveChanges`, `CommitTransaction`, `RollbackTransaction` |
| `IUnitOfWork<T>` | Interfejs | Rozszerza `IUnitOfWork` o `DataProvider` (dostęp do DbContext) |
| `IUnitOfWorkContext` | Interfejs | Niskopoziomowe operacje: `BeginTransaction`, `SaveChanges`, `DiscardChanges` |
| `UnitOfWorkContext<T>` | Klasa | Implementacja EF Core `IUnitOfWorkContext` |
| `ITransaction` | Interfejs | Transakcja: `Commit()`, `Rollback()`, `Dispose()` |
| `IHook` | Interfejs | Bazowy kontrakt hooków cyklu życia UoW: `Execute()` |
| `IPreSaveChangesHook`, `IPostSaveChangesHook`, `ITransactionCommitHook`, `ITransactionRollbackHook` | Interfejsy | Cztery rodzaje hooków (wszystkie dziedziczą po `IHook`): przed / po `SaveChanges`, po commicie, po rollbacku |
| `IUnitOfWorkOptionsBuilder` | Interfejs | Zwracany przez `AddUnitOfWork`: `WithPreSaveChangesHook<T>()`, `WithPostSaveChangesHook<T>()`, `WithTransactionCommitHook<T>()`, `WithTransactionRollbackHook<T>()` |
| `IHooksManager` | Interfejs | Zarządza kolekcją hooków i ich wykonywaniem |
| `UnitOfWorkOptionsBuilderExtensions.WithUnitOfWorkAudit<TDbContext, TAuditor>` | Metoda ext. | Włącza audyt ze wskazanym `IEntityChangesAuditor` |
| `IAuditSettings` | Interfejs | Konfiguracja audytu: `TypesToCollect`, `EntityStatesToCollect` |
| `IEntityChangesAuditor` | Interfejs | Twój odbiorca zebranych zmian: `AuditChanges(IEnumerable<EntityChange>)`, wywoływane po commicie |
| `EntityChange` | Klasa | Opis zmiany: `EntityType`, `State`, `Original`, `Current`, `PrimaryKeys` |
| `AuditEntityState` | Enum | Added, Modified, Deleted |

---

## 🔒 Bezpieczeństwo zapytań

- **Rozmiar strony jest ograniczony.** `PagingQuery<T>` odrzuca (`ArgumentOutOfRangeException`) `Page < 1`, `Count < 1`, `Count > PagingQuery<T>.MaxPageSize` (domyślnie 1000, ustawiane globalnie lub przez konstruktor) oraz przepełnienie `Skip`. Żądanie `?count=2147483647` nie zmaterializuje już całej tabeli.
- **Pole sortowania jest walidowane.** `ApplySorting` przyjmuje wyłącznie pojedynczą nazwę właściwości (`^[A-Za-z_][A-Za-z0-9_]*$`) — `Owner.PasswordHash`, `Name, Id desc` czy wyrażenia kończą się `ArgumentException`, a komunikat nie zdradza nazw typów i kolumn. Przeciążenie `ApplySorting(sorting, allowedSortFields)` ogranicza sortowanie do jawnej listy właściwości — używaj go dla encji z wrażliwymi kolumnami.

```csharp
var page = await db.Users
    .ApplySorting(request, allowedSortFields: new[] { "Name", "CreatedDateUtc" })
    .PagingAsync(request);
```

!!! note "PagingAsync"
    Do wersji 2.0.0-preview `PagingAsync` kończyło się `InvalidOperationException` („The source 'IQueryable' doesn't implement
    'IAsyncEnumerable'”) dla każdej niepustej strony, bo materializowało opakowanie `PagingQuery<T>` zamiast zapytania EF Core.
    Poprawione: strona jest pobierana z `PagingQuery<T>.Query`. Tej samej właściwości użyj, gdy sam wywołujesz `ToListAsync`
    na `PagingQuery<T>`.

---

## 🤖 AI Agent Prompt

````markdown
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
- Odczyty, które mają działać w ramach transakcji, wymagają wcześniejszego BeginTransactionManually() — inaczej transakcja otwiera się przy pierwszym SaveChanges
- Hooki implementują IPreSaveChangesHook / IPostSaveChangesHook / ITransactionCommitHook / ITransactionRollbackHook (jedna metoda: Execute()) i są rejestrowane przez With...Hook<T>() na builderze zwracanym przez AddUnitOfWork
- Zmiany encji nie są przekazywane do hooków: zaimplementuj IEntityChangesAuditor i zarejestruj go przez WithUnitOfWorkAudit<TDbContext, TAuditor>(settings => ...), ustawiając TypesToCollect i EntityStatesToCollect
- Dla encji z wrażliwymi kolumnami zawsze podawaj allowedSortFields w ApplySorting
- Nie podnoś PagingQuery<T>.MaxPageSize bez potrzeby — limit chroni przed zrzutem całej tabeli jednym żądaniem
````
