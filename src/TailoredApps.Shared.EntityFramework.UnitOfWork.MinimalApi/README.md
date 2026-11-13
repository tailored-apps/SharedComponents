# TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi

A transaction per request for ASP.NET Core **minimal APIs**: an endpoint filter that commits the unit of work of
`TailoredApps.Shared.EntityFramework` after a successful endpoint and rolls it back after an error result or an
exception. The counterpart of the MVC filter in `TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore`.

## Installation

```bash
dotnet add package TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi
```

## Registration

```csharp
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;

builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddUnitOfWorkForMinimalApi<IShopDbContext, ShopDbContext>();
```

## Usage

```csharp
var products = app.MapGroup("/api/products").WithUnitOfWorkTransaction();

products.MapPost("/", async (NewProduct product, IUnitOfWork<IShopDbContext> unitOfWork, CancellationToken cancellationToken) =>
{
    var db = unitOfWork.DataProvider;
    db.Products.Add(new Product { Name = product.Name, Price = product.Price });

    if (await db.Products.AnyAsync(p => p.Name == product.Name, cancellationToken))
        return Results.Conflict();                                        // rolled back

    return Results.Created($"/api/products/{product.Name}", product);     // committed - no SaveChanges
});

app.MapPost("/api/transfers", handler).WithUnitOfWorkTransaction(IsolationLevel.Serializable);
```

## Good to know

- Commit after a success, rollback after an exception or a result with a status of 400 or more. A plain object, or a
  result without a status code, is a success.
- The endpoint does not call `SaveChanges`, `CommitTransaction` or `RollbackTransaction`.
- When the commit fails, the transaction is rolled back and the commit exception is thrown; when the rollback fails
  too, both are thrown in an `AggregateException`.
- The transaction opens lazily, at the first `SaveChanges` (the commit counts) or after `BeginTransactionManually()`.
- Filters run in the order they are added: with `TailoredApps.Shared.ExceptionHandling.MinimalApi`, add
  `WithExceptionHandling()` before `WithUnitOfWorkTransaction()`.
- `AddUnitOfWorkForMinimalApi` registers what `AddUnitOfWork` does; an application that already calls
  `AddUnitOfWorkForWebApi` for controllers does not register twice.
- The library depends on Entity Framework Core and is therefore not a NativeAOT target.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/EntityFramework/UnitOfWork.MinimalApi/
