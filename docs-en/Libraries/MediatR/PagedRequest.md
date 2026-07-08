# TailoredApps.Shared.MediatR.PagedRequest

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.PagedRequest)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.PagedRequest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a base `PagedAndSortedRequest<TResponse, TQuery, TModel>` class for MediatR requests that require pagination and sorting. It standardizes paging (`Page`, `Count`) and sorting (`SortField`, `SortDir`) parameters across all list queries in the application.

The class is tightly integrated with `TailoredApps.Shared.Querying` — requires `TQuery` to inherit from `QueryBase` and `TResponse` to implement `IPagedResult<TModel>`.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.PagedRequest
```

---

## Przykład użycia

### Definicja query filter, response i request

```csharp
using TailoredApps.Shared.Querying;
using TailoredApps.Shared.MediatR.PagedRequest;

// 1. Filter dziedziczy po QueryBase
public class ProductFilter : QueryBase
{
    public string NameContains { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
}

// 2. Response implementuje IPagedResult<TModel>
public class ProductListResponse : IPagedResult<ProductDto>
{
    public ICollection<ProductDto> Results { get; set; }
    public int Count { get; set; }
}

// 3. Request dziedziczy PagedAndSortedRequest
public class GetProductsQuery
    : PagedAndSortedRequest<ProductListResponse, ProductFilter, ProductDto>
{
    // Wszystkie parametry paginacji/sortowania są odziedziczone
    // Można dodać własne właściwości:
    public bool IncludeArchived { get; set; } = false;
}
```

### Handler

```csharp
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

        // Aplikacja filtrów
        if (!string.IsNullOrWhiteSpace(request.Filter?.NameContains))
            query = query.Where(p => p.Name.Contains(request.Filter.NameContains));

        if (request.Filter?.MinPrice.HasValue == true)
            query = query.Where(p => p.Price >= request.Filter.MinPrice.Value);

        // Sortowanie
        if (request.IsSortingSpecified)
        {
            query = request.SortDir == SortDirection.Asc
                ? query.OrderBy(request.SortField)
                : query.OrderByDescending(request.SortField);
        }

        var totalCount = await query.CountAsync(ct);

        // Paginacja
        if (request.IsPagingSpecified)
            query = query.Skip((request.Page!.Value - 1) * request.Count!.Value)
                         .Take(request.Count.Value);

        var items = await query.Select(p => p.ToDto()).ToListAsync(ct);

        return new ProductListResponse
        {
            Results = items,
            Count = totalCount
        };
    }
}
```

### Wywołanie z kontrolera

```csharp
[HttpGet]
public async Task<IActionResult> GetProducts([FromQuery] GetProductsQuery query)
{
    // GET /api/products?page=1&count=20&sortField=Price&sortDir=Asc&filter.nameContains=shirt
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `PagedAndSortedRequest<TResponse, TQuery, TModel>` | Klasa bazowa | Bazowy request MediatR z paginacją i sortowaniem |
| `Page` | Właściwość `int?` | Numer strony (1-based) |
| `Count` | Właściwość `int?` | Liczba elementów na stronie |
| `IsPagingSpecified` | Właściwość `bool` | `true` gdy Page i Count mają wartość |
| `SortField` | Właściwość `string` | Nazwa pola do sortowania |
| `SortDir` | Właściwość `SortDirection?` | Kierunek sortowania (Asc/Desc) |
| `IsSortingSpecified` | Właściwość `bool` | `true` gdy SortField i SortDir są ustawione |
| `Filter` | Właściwość `TQuery` | Obiekt filtra dziedziczący po `QueryBase` |
| `IsSortBy(string)` | Metoda | Sprawdza czy sortowanie jest po danym polu (case-insensitive) |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.MediatR.PagedRequest — Instrukcja dla agenta AI

Używasz PagedAndSortedRequest jako bazowej klasy dla stronicowanych requestów MediatR.

### Definicja
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

### Parametry URL (ASP.NET Core binding)
?page=1&count=20&sortField=Name&sortDir=Asc&filter.name=test

### W handlerze
```csharp
if (request.IsPagingSpecified)
    query = query.Skip((request.Page!.Value - 1) * request.Count!.Value).Take(request.Count.Value);

if (request.IsSortingSpecified)
    query = request.SortDir == SortDirection.Asc ? query.OrderBy(...) : query.OrderByDescending(...);
```

### Zasady
- TQuery musi dziedziczyć po QueryBase
- TResponse musi implementować IPagedResult<TModel>
- IsPagingSpecified = Page i Count mają wartość — sprawdzaj przed Skip/Take
- IsSortBy("Name") — sprawdza case-insensitive
```
