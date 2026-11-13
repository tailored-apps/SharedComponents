# TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi)](https://www.nuget.org/packages/TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

A transaction per request for **minimal APIs**: an endpoint filter that ends an endpoint with a commit or a rollback of the unit of work of
`TailoredApps.Shared.EntityFramework`. It is the counterpart of the MVC filter in
[UnitOfWork.WebApiCore](UnitOfWork.WebApiCore.md) - with the same rules - for applications without controllers. The library is a package
of its own; it changes nothing in the existing ones.

- **Commits** after the endpoint succeeds (pending changes are saved first) - the endpoint does not call `SaveChanges`
- **Rolls back** after an exception, or when the endpoint returned a result with a status of 400 or more (`Results.BadRequest()`, `Results.Conflict()`, …)
- An endpoint that returns a plain object, or a result without a status code, counts as a success
- Optionally sets the **isolation level** for an endpoint or a group

!!! note "NativeAOT"
    The filter itself uses no reflection, but the library depends on Entity Framework Core, which is not ready for NativeAOT
    (see [Trimming and NativeAOT](../../Platform/index.md#trimming-and-nativeaot)). Minimal APIs without AOT work without restrictions.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddUnitOfWorkForMinimalApi<IShopDbContext, ShopDbContext>();
```

`AddUnitOfWorkForMinimalApi` registers what `AddUnitOfWork` of `TailoredApps.Shared.EntityFramework` does and returns the same builder
(hooks, auditing). An application that already calls `AddUnitOfWorkForWebApi` for its controllers does not register the unit of work twice.

---

## Usage example

```csharp
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

// A whole group in a transaction
var products = app.MapGroup("/api/products").WithUnitOfWorkTransaction();

products.MapPost("/", async (NewProduct product, IUnitOfWork<IShopDbContext> unitOfWork, CancellationToken cancellationToken) =>
{
    var db = unitOfWork.DataProvider;
    db.Products.Add(new Product { Name = product.Name, Price = product.Price });

    // The product is already in the context. Answering 409 makes the filter roll it back.
    if (await db.Products.AnyAsync(p => p.Name == product.Name, cancellationToken))
        return Results.Conflict();

    return Results.Created($"/api/products/{product.Name}", product);   // commit - no SaveChanges
});

// One endpoint with its own isolation level
app.MapPost("/api/transfers", (Transfer transfer, IUnitOfWork<IShopDbContext> unitOfWork) => { /* ... */ })
   .WithUnitOfWorkTransaction(IsolationLevel.Serializable);
```

Filters run in the order they are added. With [ExceptionHandling.MinimalApi](../ExceptionHandling/MinimalApi.md) add exception handling
**first**, so that it also answers for an exception thrown by the commit:

```csharp
app.MapGroup("/api/products")
   .WithExceptionHandling()          // outer: turns an exception into the error response
   .WithUnitOfWorkTransaction();     // inner: commit / rollback
```

---

## API Reference

| Type | Kind | Description |
|------|------|-------------|
| `UnitOfWorkMinimalApiExtensions.AddUnitOfWorkForMinimalApi<TI, T>` | Ext. method | Registers the unit of work for a minimal-API application |
| `UnitOfWorkMinimalApiExtensions.WithUnitOfWorkTransaction<TBuilder>(isolationLevel)` | Ext. method | Adds the transaction filter to an endpoint (`RouteHandlerBuilder`) or a group (`RouteGroupBuilder`) |
| `UnitOfWorkTransactionEndpointFilter` | Endpoint filter | Commit after a success, rollback after an exception or a result with a status ≥ 400 |

---

## 🔒 Transaction consistency

- **Rollback on an error response**, not only on an exception: partial writes made before `return Results.Conflict()` do not stay in the database.
- **A failed commit is not lost.** When the commit fails the transaction is rolled back and the commit exception is thrown; when the
  rollback fails too, both end up in an `AggregateException`.
- **The transaction opens lazily** - at the first `SaveChanges` / `SaveChangesAsync` (the commit at the end counts) or after
  `BeginTransactionManually()`. Call `BeginTransactionManually()` before a read that has to be protected by the transaction.
- **Isolation levels:** `ReadCommitted` by default; `ReadCommitted`, `ReadUncommitted` and `Serializable` are supported.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi - instructions for an AI agent

You are using a transaction per request in ASP.NET Core minimal APIs through an endpoint filter.

### Registration
```csharp
// Program.cs
builder.Services.AddDbContext<MyDbContext>(...);
builder.Services.AddUnitOfWorkForMinimalApi<IMyDbContext, MyDbContext>();

app.MapGroup("/api/orders").WithUnitOfWorkTransaction();                  // a whole group
app.MapPost("/api/transfers", handler).WithUnitOfWorkTransaction(IsolationLevel.Serializable);
```

### What gets configured
- IUnitOfWork and IUnitOfWork<IMyDbContext> (scoped) - inject IUnitOfWork<IMyDbContext> into the endpoint
- The endpoint filter: success -> CommitTransaction() (saves the changes), an exception or a result with a status >= 400 -> RollbackTransaction()

### Configuration
- No configuration keys. The isolation level is passed to WithUnitOfWorkTransaction(isolationLevel)

### Rules
- Do not call SaveChanges, CommitTransaction or RollbackTransaction in an endpoint - the filter does
- Return a business failure as a 4xx result (Results.Conflict(), Results.BadRequest()) - the filter rolls the changes back
- With ExceptionHandling.MinimalApi add WithExceptionHandling() before WithUnitOfWorkTransaction()
- Do not register the unit of work twice: in an application with controllers AddUnitOfWorkForWebApi is enough
- For MVC controllers use the UnitOfWork.WebApiCore package, not this one
- The library depends on EF Core: do not use it in an application published with PublishAot
````
