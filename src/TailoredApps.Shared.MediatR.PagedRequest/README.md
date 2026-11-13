# TailoredApps.Shared.MediatR.PagedRequest

`PagedAndSortedRequest<TResponse, TQuery, TModel>`: a base class for MediatR requests that return one page of a
list. It carries the page, the page size, the sort field and direction and a filter object, so every list
request in an application has the same shape. The contracts it implements come from `TailoredApps.Shared.Querying`.

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.PagedRequest
```

## Registration

Nothing to register: the package has no dependency-injection extension. Requests derived from the base class
are ordinary MediatR requests (`IRequest<TResponse>`).

## Usage

The handler below uses the paging and sorting extensions of `TailoredApps.Shared.EntityFramework`, which accept
the request directly and validate what the client sent.

```csharp
using TailoredApps.Shared.EntityFramework.Querying;
using TailoredApps.Shared.MediatR.PagedRequest;
using TailoredApps.Shared.Querying;

public class ProductFilter : QueryBase
{
    public string? NameContains { get; set; }
}

public class GetProducts : PagedAndSortedRequest<PagedResult<ProductDto>, ProductFilter, ProductDto>
{
}

public class GetProductsHandler(ShopDbContext db) : IRequestHandler<GetProducts, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(GetProducts request, CancellationToken cancellationToken)
    {
        IQueryable<Product> products = db.Products;

        var name = request.Filter?.NameContains;
        if (!string.IsNullOrWhiteSpace(name))
        {
            products = products.Where(p => p.Name.Contains(name));
        }

        var page = await products
            .ApplySorting(request, ["Name", "Price"])   // allow-list of sort fields
            .PagingAsync(request);

        return page.Project(p => new ProductDto(p.Id, p.Name, p.Price));
    }
}
```

| Member | Meaning |
| --- | --- |
| `Page`, `Count` | Page number (1-based) and page size; `null` means no paging |
| `IsPagingSpecified` | `true` when both `Page` and `Count` have a value |
| `SortField`, `SortDir` | Field to sort by and `SortDirection` (`Asc`, `Desc`) |
| `IsSortingSpecified` | `true` when `SortField` is not blank and `SortDir` has a value |
| `Filter` | The filter object, a class derived from `QueryBase` |
| `IsSortBy(fieldName)` | Compares `SortField` with a field name, ignoring case |

## Good to know

- Built on [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a fork of MediatR 12.5.0 under the Apache-2.0 licence, not on the MediatR package: requests and handlers use the `ForkForward.Mediator` namespace and the mediator is registered with `AddMediator(...)`. "MediatR" in the name of this library is its history, not its dependency.
- `TQuery` must derive from `QueryBase` and `TResponse` must implement `IPagedResult<TModel>`.
- The class only carries the values. `Page`, `Count` and `SortField` usually come straight from the HTTP
  request and are not checked here: a handler that calls `Skip`/`Take` itself has to limit the page size.
- `PagingAsync` and `ApplySorting` in `TailoredApps.Shared.EntityFramework` do check them: the page must be at
  least 1, the page size between 1 and `PagingQuery<T>.MaxPageSize` (1000 by default), and the sort field a
  single property name, optionally from an allow-list. Violations throw `ArgumentOutOfRangeException` or
  `ArgumentException`.
- `IsSortingSpecified` is also `true` for `SortDirection.Undefined`.
- The types of this package are safe for trimming and NativeAOT; the EntityFramework extensions used in the handler above are not (EF Core).

## Documentation

https://shared.tailoredapps.pl/en/Libraries/MediatR/PagedRequest/
