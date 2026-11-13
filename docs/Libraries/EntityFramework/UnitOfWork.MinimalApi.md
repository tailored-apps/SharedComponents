# TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Transakcja na żądanie dla **minimal API**: filtr endpointu, który kończy pracę endpointu commitem albo rollbackiem Unit of Work z
`TailoredApps.Shared.EntityFramework`. To odpowiednik filtra MVC z
[UnitOfWork.WebApiCore](UnitOfWork.WebApiCore.md) — z tymi samymi regułami — dla aplikacji bez kontrolerów. Biblioteka jest osobną paczką;
niczego nie zmienia w istniejących.

- **Commituje** po pomyślnym wykonaniu endpointu (najpierw zapisuje oczekujące zmiany) — endpoint nie woła `SaveChanges`
- **Rollbackuje** po wyjątku albo gdy endpoint zwrócił wynik ze statusem 400 lub wyższym (`Results.BadRequest()`, `Results.Conflict()`, …)
- Endpoint, który zwraca zwykły obiekt albo wynik bez kodu statusu, liczy się jako sukces
- Opcjonalnie ustawia **poziom izolacji** dla endpointu lub grupy

!!! note "NativeAOT"
    Sam filtr nie używa refleksji, ale biblioteka zależy od Entity Framework Core, który nie jest gotowy na NativeAOT
    (zob. [Trimming i NativeAOT](../../Platform/index.md#trimming-i-nativeaot)). Minimal API bez AOT działa bez ograniczeń.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddUnitOfWorkForMinimalApi<IShopDbContext, ShopDbContext>();
```

`AddUnitOfWorkForMinimalApi` rejestruje to samo co `AddUnitOfWork` z `TailoredApps.Shared.EntityFramework` i zwraca ten sam builder
(hooki, audyt). Aplikacja, która ma już `AddUnitOfWorkForWebApi` dla kontrolerów, nie rejestruje Unit of Work drugi raz.

---

## Przykład użycia

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

// Cała grupa w transakcji
var products = app.MapGroup("/api/products").WithUnitOfWorkTransaction();

products.MapPost("/", async (NewProduct product, IUnitOfWork<IShopDbContext> unitOfWork, CancellationToken cancellationToken) =>
{
    var db = unitOfWork.DataProvider;
    db.Products.Add(new Product { Name = product.Name, Price = product.Price });

    // Produkt jest już w kontekście. Odpowiedź 409 sprawia, że filtr go wycofa.
    if (await db.Products.AnyAsync(p => p.Name == product.Name, cancellationToken))
        return Results.Conflict();

    return Results.Created($"/api/products/{product.Name}", product);   // commit - bez SaveChanges
});

// Jeden endpoint z własnym poziomem izolacji
app.MapPost("/api/transfers", (Transfer transfer, IUnitOfWork<IShopDbContext> unitOfWork) => { /* ... */ })
   .WithUnitOfWorkTransaction(IsolationLevel.Serializable);
```

Filtry działają w kolejności dodania. Z [ExceptionHandling.MinimalApi](../ExceptionHandling/MinimalApi.md) dodaj obsługę wyjątków
**pierwszą**, żeby odpowiadała także za wyjątek z commitu:

```csharp
app.MapGroup("/api/products")
   .WithExceptionHandling()          // zewnętrzny: zamienia wyjątek na odpowiedź błędu
   .WithUnitOfWorkTransaction();     // wewnętrzny: commit / rollback
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `UnitOfWorkMinimalApiExtensions.AddUnitOfWorkForMinimalApi<TI, T>` | Metoda ext. | Rejestruje Unit of Work dla aplikacji minimal API |
| `UnitOfWorkMinimalApiExtensions.WithUnitOfWorkTransaction<TBuilder>(isolationLevel)` | Metoda ext. | Dodaje filtr transakcji do endpointu (`RouteHandlerBuilder`) albo grupy (`RouteGroupBuilder`) |
| `UnitOfWorkTransactionEndpointFilter` | Filtr endpointu | Commit po sukcesie, rollback po wyjątku lub wyniku ze statusem ≥ 400 |

---

## 🔒 Spójność transakcji

- **Rollback przy odpowiedzi błędu**, nie tylko przy wyjątku: częściowe zapisy sprzed `return Results.Conflict()` nie zostają w bazie.
- **Błąd commitu nie ginie.** Gdy commit zawiedzie, transakcja jest wycofywana i zgłaszany jest wyjątek commitu; gdy zawiedzie też
  rollback, oba trafiają do `AggregateException`.
- **Transakcja otwiera się leniwie** — przy pierwszym `SaveChanges` / `SaveChangesAsync` (liczy się commit na końcu) albo po
  `BeginTransactionManually()`. Odczyt, który ma być chroniony transakcją, poprzedź `BeginTransactionManually()`.
- **Poziomy izolacji:** domyślny `ReadCommitted`; obsługiwane `ReadCommitted`, `ReadUncommitted` i `Serializable`.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi — Instrukcja dla agenta AI

Używasz transakcji na żądanie w ASP.NET Core minimal API przez filtr endpointu.

### Rejestracja
```csharp
// Program.cs
builder.Services.AddDbContext<MyDbContext>(...);
builder.Services.AddUnitOfWorkForMinimalApi<IMyDbContext, MyDbContext>();

app.MapGroup("/api/orders").WithUnitOfWorkTransaction();                  // cała grupa
app.MapPost("/api/transfers", handler).WithUnitOfWorkTransaction(IsolationLevel.Serializable);
```

### Co zostaje skonfigurowane
- IUnitOfWork i IUnitOfWork<IMyDbContext> (scoped) - wstrzykuj IUnitOfWork<IMyDbContext> do endpointu
- Filtr endpointu: sukces -> CommitTransaction() (zapisuje zmiany), wyjątek lub wynik ze statusem >= 400 -> RollbackTransaction()

### Konfiguracja
- Brak kluczy konfiguracji. Poziom izolacji podaje się w WithUnitOfWorkTransaction(isolationLevel)

### Zasady
- Nie wywołuj SaveChanges, CommitTransaction ani RollbackTransaction w endpoincie - filtr to robi
- Niepowodzenie biznesowe zwracaj jako wynik 4xx (Results.Conflict(), Results.BadRequest()) - filtr wycofa zmiany
- Z ExceptionHandling.MinimalApi dodaj WithExceptionHandling() przed WithUnitOfWorkTransaction()
- Nie rejestruj Unit of Work dwa razy: w aplikacji z kontrolerami wystarcza AddUnitOfWorkForWebApi
- Dla kontrolerów MVC użyj paczki UnitOfWork.WebApiCore, nie tej
- Biblioteka zależy od EF Core: nie używaj jej w aplikacji publikowanej z PublishAot
````
