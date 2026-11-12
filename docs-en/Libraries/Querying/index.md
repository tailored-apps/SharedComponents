# TailoredApps.Shared.Querying

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Querying)](https://www.nuget.org/packages/TailoredApps.Shared.Querying/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a set of base classes and interfaces for building paged and sorted queries in .NET applications. It standardizes the structure of list queries across the entire application — instead of passing `page`, `pageSize`, `sortField` as separate parameters everywhere, you have one consistent contract.

Key types:
- **`QueryBase`** — abstract base class for query filter objects
- **`PagedAndSortedQuery<TQuery>`** — base class combining filtering, pagination, and sorting
- **`IPagedResult<T>`** — result contract with pagination: collection + total count
- **`SortDirection`** — `Asc`/`Desc`/`Undefined` enum

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Querying
```

---

## Usage example

### Defining the filter and the query

```csharp
using TailoredApps.Shared.Querying;

// 1. Filter — the specific search criteria
public class CustomerFilter : QueryBase
{
    public string NameContains { get; set; }
    public string Email { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? RegisteredAfter { get; set; }
}

// 2. Paged + sorted query
public class GetCustomersQuery : PagedAndSortedQuery<CustomerFilter>
{
    // All parameters are inherited:
    // Page, Count, SortField, SortDir, Filter, IsPagingSpecified, IsSortingSpecified
}

// 3. Paged result
public class CustomerListResult : IPagedResult<CustomerDto>
{
    public ICollection<CustomerDto> Results { get; set; }
    public int Count { get; set; }  // total count (without paging)
}
```

### Implementation in a repository / service

```csharp
public async Task<CustomerListResult> GetCustomersAsync(
    GetCustomersQuery query,
    CancellationToken ct = default)
{
    var dbQuery = _context.Customers.AsQueryable();

    // Filters
    if (!string.IsNullOrWhiteSpace(query.Filter?.NameContains))
        dbQuery = dbQuery.Where(c => c.Name.Contains(query.Filter.NameContains));

    if (!string.IsNullOrWhiteSpace(query.Filter?.Email))
        dbQuery = dbQuery.Where(c => c.Email == query.Filter.Email);

    if (query.Filter?.IsActive.HasValue == true)
        dbQuery = dbQuery.Where(c => c.IsActive == query.Filter.IsActive.Value);

    // Sorting
    if (query.IsSortingSpecified)
    {
        // Example with IsSortBy()
        if (query.IsSortBy("Name"))
            dbQuery = query.SortDir == SortDirection.Asc
                ? dbQuery.OrderBy(c => c.Name)
                : dbQuery.OrderByDescending(c => c.Name);
        else if (query.IsSortBy("RegisteredAt"))
            dbQuery = query.SortDir == SortDirection.Asc
                ? dbQuery.OrderBy(c => c.RegisteredAt)
                : dbQuery.OrderByDescending(c => c.RegisteredAt);
    }

    var totalCount = await dbQuery.CountAsync(ct);

    // Paging
    if (query.IsPagingSpecified)
    {
        var skip = (query.Page!.Value - 1) * query.Count!.Value;
        dbQuery = dbQuery.Skip(skip).Take(query.Count.Value);
    }

    var items = await dbQuery
        .Select(c => new CustomerDto { Id = c.Id, Name = c.Name, Email = c.Email })
        .ToListAsync(ct);

    return new CustomerListResult
    {
        Results = items,
        Count = totalCount
    };
}
```

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `QueryBase` | Abstract class | Base class for all query filter objects |
| `PagedAndSortedQuery<TQuery>` | Abstract class | Base query: paging + sorting + filter |
| `IPagedAndSortedQuery<TQuery>` | Interface | `PagedAndSortedQuery` contract |
| `IPagedResult<T>` | Interface | Paged result: `Results` + `Count` |
| `SortDirection` | Enum | `Undefined = 0`, `Asc = 1`, `Desc = 2`. "No sorting" is a `null` `SortDir`; `IsSortingSpecified` is `true` for any value, including `Undefined` |
| `IPagingParameters` | Interface | `Page`, `Count`, `IsPagingSpecified` |
| `ISortingParameters` | Interface | `SortField`, `SortDir`, `IsSortingSpecified` |
| `IQueryParameters` | Interface | Combines `IPagingParameters` + `ISortingParameters` |
| `IQuery<T>` | Interface | Query with a filter object: `Filter` |
| `IPagedAndSortedRequest<TResponse, TQuery, TModel>` | Interface | MediatR request contract with paging (used by MediatR.PagedRequest) |
| `IsSortBy(string)` | Method | Case-insensitive comparison with `SortField` |
| `QueryMap<TDestination, TSource>` | Class | A pair of expressions, `Destination` (a field of the model/DTO) and `Source` (the matching field of the entity). It only holds them - nothing in this package interprets the mapping |

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.Querying - instructions for an AI agent

You are using TailoredApps.Shared.Querying to standardize paged queries.

### Type definitions
```csharp
// The filter inherits from QueryBase
public class MyFilter : QueryBase { public string Name { get; set; } }

// The query inherits from PagedAndSortedQuery<TFilter>
public class GetItemsQuery : PagedAndSortedQuery<MyFilter> { }

// The result implements IPagedResult<TItem>
public class ItemsResult : IPagedResult<ItemDto>
{
    public ICollection<ItemDto> Results { get; set; }
    public int Count { get; set; }
}
```

### URL parameters → automatic binding in ASP.NET Core
?page=1&count=20&sortField=Name&sortDir=Asc&filter.name=test

### In the service/repository
```csharp
if (query.IsPagingSpecified)
    dbQuery = dbQuery.Skip((query.Page!.Value - 1) * query.Count!.Value).Take(query.Count.Value);

if (query.IsSortingSpecified)
{
    if (query.IsSortBy("Name"))
        dbQuery = query.SortDir == SortDirection.Asc ? dbQuery.OrderBy(x => x.Name) : dbQuery.OrderByDescending(x => x.Name);
}
```

### Rules
- Count in IPagedResult = the total count without paging (for calculating pages in the UI)
- No sorting = SortDir is null (the default). SortDirection.Undefined is only the enum's zero value: IsSortingSpecified is still true for it when SortField is set, so handle it explicitly
- IsSortBy() compares case-insensitively — always use it instead of a manual string.Equals
- IsPagingSpecified = BOTH values, Page and Count, must be != null
````
