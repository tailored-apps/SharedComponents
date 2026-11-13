# TailoredApps.Shared.MediatR.PagedRequest

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.PagedRequest)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.PagedRequest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a base `PagedAndSortedRequest<TResponse, TQuery, TModel>` class for MediatR requests that require pagination and sorting. It standardizes paging (`Page`, `Count`) and sorting (`SortField`, `SortDir`) parameters across all list queries in the application.

The class is tightly integrated with `TailoredApps.Shared.Querying` — requires `TQuery` to inherit from `QueryBase` and `TResponse` to implement `IPagedResult<TModel>`.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.PagedRequest
```

---

## Usage example

### Defining the query filter, response and request

```csharp
using TailoredApps.Shared.Querying;
using TailoredApps.Shared.MediatR.PagedRequest;

// 1. The filter inherits from QueryBase
public class ProductFilter : QueryBase
{
    public string NameContains { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
}

// 2. The response implements IPagedResult<TModel>
public class ProductListResponse : IPagedResult<ProductDto>
{
    public ICollection<ProductDto> Results { get; set; }
    public int Count { get; set; }
}

// 3. The request inherits from PagedAndSortedRequest
public class GetProductsQuery
    : PagedAndSortedRequest<ProductListResponse, ProductFilter, ProductDto>
{
    // All paging/sorting parameters are inherited
    // You can add your own properties:
    public bool IncludeArchived { get; set; } = false;
}
```

### Handler

The handler uses `ApplySorting` and `PagingAsync` from the `TailoredApps.Shared.EntityFramework` package. Both accept the request directly (it implements `ISortingParameters` and `IPagingParameters`) and validate the values the client sent.

```csharp
using TailoredApps.Shared.EntityFramework.Querying;

public class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, ProductListResponse>
{
    private readonly IProductRepository _repo;

    public GetProductsQueryHandler(IProductRepository repo) => _repo = repo;

    public async Task<ProductListResponse> Handle(
        GetProductsQuery request,
        CancellationToken ct)
    {
        var query = _repo.AsQueryable();

        // Apply the filters
        if (!string.IsNullOrWhiteSpace(request.Filter?.NameContains))
            query = query.Where(p => p.Name.Contains(request.Filter.NameContains));

        if (request.Filter?.MinPrice.HasValue == true)
            query = query.Where(p => p.Price >= request.Filter.MinPrice.Value);

        // Sorting: SortField comes from the client, so it is checked against an allow-list.
        // Paging: validates Page and Count, counts all matching rows and reads one page
        // (the whole result when the request carries no paging values).
        var page = await query
            .ApplySorting(request, allowedSortFields: new[] { "Name", "Price" })
            .PagingAsync(request);

        return new ProductListResponse
        {
            Results = page.Results.Select(p => p.ToDto()).ToList(),
            Count = page.Count   // total number of matching rows
        };
    }
}
```

### Calling from a controller

```csharp
[HttpGet]
public async Task<IActionResult> GetProducts([FromQuery] GetProductsQuery query)
{
    // GET /api/products?page=1&count=20&sortField=Price&sortDir=Asc&filter.nameContains=shirt
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

!!! info "Trimming and NativeAOT"
    The types of this package (`PagedAndSortedRequest<,,>`, `IPagedResult<>`) are safe for trimming and NativeAOT; the handler is then
    registered by name - see [MediatR: Trimming and NativeAOT](index.md#trimming-and-nativeaot). The paging and sorting extensions
    used in the example come from `TailoredApps.Shared.EntityFramework`, which is not NativeAOT-compatible (EF Core).

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `PagedAndSortedRequest<TResponse, TQuery, TModel>` | Base class | Base MediatR request with paging and sorting |
| `Page` | Property `int?` | Page number (1-based) |
| `Count` | Property `int?` | Number of items per page |
| `IsPagingSpecified` | Property `bool` | `true` when Page and Count have a value |
| `SortField` | Property `string` | Name of the field to sort by |
| `SortDir` | Property `SortDirection?` | Sort direction (Asc/Desc) |
| `IsSortingSpecified` | Property `bool` | `true` when SortField is not blank and SortDir has a value (also for `SortDirection.Undefined`) |
| `Filter` | Property `TQuery` | Filter object inheriting from `QueryBase` |
| `IsSortBy(string)` | Method | Checks whether sorting is by the given field (case-insensitive) |

---

## 🔒 Security

`Page`, `Count` and `SortField` come straight from the HTTP request. The EF layer (`PagingQuery<T>`, `ApplySorting`) enforces `Page >= 1`, `1 <= Count <= PagingQuery<T>.MaxPageSize` (default 1000) and a single property name as the sort field; violations throw `ArgumentException`/`ArgumentOutOfRangeException`. For entities with sensitive columns pass an allow-list of sort fields to `ApplySorting`.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR.PagedRequest - instructions for an AI agent

You are using PagedAndSortedRequest as the base class for paged MediatR requests.

### Definition
```csharp
// Filter
public class MyFilter : QueryBase { public string Name { get; set; } }

// Response  
public class MyListResponse : IPagedResult<MyDto>
{
    public ICollection<MyDto> Results { get; set; }
    public int Count { get; set; }
}

// Request
public class GetMyItemsQuery : PagedAndSortedRequest<MyListResponse, MyFilter, MyDto> { }
```

### URL parameters (ASP.NET Core binding)
?page=1&count=20&sortField=Name&sortDir=Asc&filter.name=test

### In the handler
```csharp
// using TailoredApps.Shared.EntityFramework.Querying;  (package TailoredApps.Shared.EntityFramework)
var page = await query
    .ApplySorting(request, allowedSortFields: new[] { "Name" })   // validates SortField
    .PagingAsync(request);                                        // validates Page/Count, returns PagedResult<T>
// page.Results - the items of the page, page.Count - the total
```

### Rules
- TQuery must inherit from QueryBase
- TResponse must implement IPagedResult<TModel>
- IsPagingSpecified = Page and Count have a value — PagingAsync pages only then; if you call Skip/Take yourself, check it and cap Count
- IsSortBy("Name") — the check is case-insensitive
- Never hand SortField to Dynamic LINQ yourself - use ApplySorting with an allow-list of fields
- An application with PublishAot: the types of the package work, register the handler with cfg.AddRequestHandler<,,>(); the extensions of TailoredApps.Shared.EntityFramework (EF Core) are not NativeAOT-compatible
````
