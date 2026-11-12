# TailoredApps.Shared.Querying

Contracts for list queries: a query with paging, sorting and a filter object, and a result with one page of
items plus the total count. The package has no dependencies, so the same types can be used in an API contract,
in a service and in the data layer. `TailoredApps.Shared.EntityFramework` and
`TailoredApps.Shared.MediatR.PagedRequest` build on these contracts.

## Installation

```bash
dotnet add package TailoredApps.Shared.Querying
```

## Registration

Nothing to register: the package contains contracts only and has no dependency-injection extension.

## Usage

```csharp
using TailoredApps.Shared.Querying;

public class CustomerFilter : QueryBase
{
    public string? NameContains { get; set; }
}

public class CustomerQuery : PagedAndSortedQuery<CustomerFilter>
{
}

public class CustomerPage : IPagedResult<CustomerDto>
{
    public ICollection<CustomerDto> Results { get; set; } = new List<CustomerDto>();
    public int Count { get; set; }   // total number of matching items, not the size of the page
}
```

Applying a query (EF Core shown; `maxPageSize` is a limit you choose):

```csharp
if (query.IsSortingSpecified && query.IsSortBy("Name"))
{
    customers = query.SortDir == SortDirection.Desc
        ? customers.OrderByDescending(c => c.Name)
        : customers.OrderBy(c => c.Name);
}

var total = await customers.CountAsync(cancellationToken);

if (query.IsPagingSpecified)
{
    var size = Math.Clamp(query.Count!.Value, 1, maxPageSize);
    customers = customers.Skip((Math.Max(query.Page!.Value, 1) - 1) * size).Take(size);
}
```

| Type | Purpose |
| --- | --- |
| `QueryBase` | Abstract base class of filter objects |
| `PagedAndSortedQuery<TQuery>` | Abstract query: `Page`, `Count`, `SortField`, `SortDir`, `Filter`, `IsPagingSpecified`, `IsSortingSpecified`, `IsSortBy(fieldName)` |
| `IPagedAndSortedQuery<TQuery>`, `IQuery<T>` | The contract of the query and of its filter |
| `IPagingParameters`, `ISortingParameters`, `IQueryParameters` | Read-only views of the paging values, the sorting values, and both |
| `IPagedResult<T>` | `Results` (the page) and `Count` (the total) |
| `IPagedAndSortedRequest<TResponse, TQuery, TModel>` | Query contract tied to a response type; implemented by `MediatR.PagedRequest` |
| `SortDirection` | `Undefined = 0`, `Asc = 1`, `Desc = 2` |
| `QueryMap<TDestination, TSource>` | A pair of expressions naming a field of a model and the matching field of an entity |

## Good to know

- `Page` is 1-based. `Page`, `Count` and `SortDir` are nullable; `IsPagingSpecified` requires both `Page` and
  `Count`, `IsSortingSpecified` requires a non-blank `SortField` and a `SortDir` value.
- `IsSortingSpecified` is also `true` for `SortDirection.Undefined`, the enum's zero value.
- `IsSortBy` compares field names ignoring case.
- Nothing here validates the values: a negative page, a huge page size or an arbitrary sort field pass through
  as given. Check them where the query is applied. The paging and sorting extensions of
  `TailoredApps.Shared.EntityFramework` do that for `IPagingParameters` and `ISortingParameters`.
- `QueryMap<TDestination, TSource>` only holds the two expressions; nothing in this package interprets them.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/Querying/
