# TailoredApps.Shared.Querying

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Querying)](https://www.nuget.org/packages/TailoredApps.Shared.Querying/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Biblioteka dostarcza zestaw bazowych klas i interfejsów do budowania stronicowanych i sortowanych zapytań w aplikacjach .NET. Standaryzuje strukturę zapytań listowych w całej aplikacji — zamiast przekazywać `page`, `pageSize`, `sortField` jako oddzielne parametry w każdym miejscu, masz jeden spójny kontrakt.

Kluczowe typy:
- **`QueryBase`** — abstrakcyjna klasa bazowa dla obiektów filtru zapytań
- **`PagedAndSortedQuery<TQuery>`** — klasa bazowa łącząca filtrowanie, paginację i sortowanie
- **`IPagedResult<T>`** — kontrakt wynikowy ze stronicowaniem: kolekcja + łączna liczba
- **`SortDirection`** — enum `Asc`/`Desc`/`Undefined`

## 🇬🇧 Description

This library provides a set of base classes and interfaces for building paged and sorted queries in .NET applications. It standardizes the structure of list queries across the entire application — instead of passing `page`, `pageSize`, `sortField` as separate parameters everywhere, you have one consistent contract.

Key types:
- **`QueryBase`** — abstract base class for query filter objects
- **`PagedAndSortedQuery<TQuery>`** — base class combining filtering, pagination, and sorting
- **`IPagedResult<T>`** — result contract with pagination: collection + total count
- **`SortDirection`** — `Asc`/`Desc`/`Undefined` enum

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Querying
```

---

## Przykład użycia

### Definicja filtru i zapytania

```csharp
using TailoredApps.Shared.Querying;

// 1. Filter — konkretne kryteria wyszukiwania
public class CustomerFilter : QueryBase
{
    public string NameContains { get; set; }
    public string Email { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? RegisteredAfter { get; set; }
}

// 2. Zapytanie stronicowane + sortowane
public class GetCustomersQuery : PagedAndSortedQuery<CustomerFilter>
{
    // Wszystkie parametry są odziedziczone:
    // Page, Count, SortField, SortDir, Filter, IsPagingSpecified, IsSortingSpecified
}

// 3. Wynik stronicowany
public class CustomerListResult : IPagedResult<CustomerDto>
{
    public ICollection<CustomerDto> Results { get; set; }
    public int Count { get; set; }  // łączna liczba (bez stronicowania)
}
```

### Implementacja w repozytorium / serwisie

```csharp
public async Task<CustomerListResult> GetCustomersAsync(
    GetCustomersQuery query,
    CancellationToken ct = default)
{
    var dbQuery = _context.Customers.AsQueryable();

    // Filtry
    if (!string.IsNullOrWhiteSpace(query.Filter?.NameContains))
        dbQuery = dbQuery.Where(c => c.Name.Contains(query.Filter.NameContains));

    if (!string.IsNullOrWhiteSpace(query.Filter?.Email))
        dbQuery = dbQuery.Where(c => c.Email == query.Filter.Email);

    if (query.Filter?.IsActive.HasValue == true)
        dbQuery = dbQuery.Where(c => c.IsActive == query.Filter.IsActive.Value);

    // Sortowanie
    if (query.IsSortingSpecified)
    {
        // Przykład z IsSortBy()
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

    // Paginacja
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

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `QueryBase` | Klasa abstrakcyjna | Klasa bazowa dla wszystkich obiektów filtrów zapytań |
| `PagedAndSortedQuery<TQuery>` | Klasa abstrakcyjna | Bazowe zapytanie: paginacja + sortowanie + filtr |
| `IPagedAndSortedQuery<TQuery>` | Interfejs | Kontrakt `PagedAndSortedQuery` |
| `IPagedResult<T>` | Interfejs | Wynik stronicowany: `Results` + `Count` |
| `SortDirection` | Enum | `Undefined = 0`, `Asc = 1`, `Desc = 2` |
| `IPagingParameters` | Interfejs | `Page`, `Count`, `IsPagingSpecified` |
| `ISortingParameters` | Interfejs | `SortField`, `SortDir`, `IsSortingSpecified` |
| `IQueryParameters` | Interfejs | Łączy `IPagingParameters` + `ISortingParameters` |
| `IQuery<T>` | Interfejs | Zapytanie z obiektem filtru: `Filter` |
| `IPagedAndSortedRequest<TResponse, TQuery, TModel>` | Interfejs | Kontrakt MediatR request z paginacją (używany przez MediatR.PagedRequest) |
| `IsSortBy(string)` | Metoda | Case-insensitive porównanie z `SortField` |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Querying — Instrukcja dla agenta AI

Używasz TailoredApps.Shared.Querying do standaryzacji stronicowanych zapytań.

### Definicja typów
```csharp
// Filter dziedziczy QueryBase
public class MyFilter : QueryBase { public string Name { get; set; } }

// Zapytanie dziedziczy PagedAndSortedQuery<TFilter>
public class GetItemsQuery : PagedAndSortedQuery<MyFilter> { }

// Wynik implementuje IPagedResult<TItem>
public class ItemsResult : IPagedResult<ItemDto>
{
    public ICollection<ItemDto> Results { get; set; }
    public int Count { get; set; }
}
```

### Parametry URL → automatyczny binding w ASP.NET Core
?page=1&count=20&sortField=Name&sortDir=Asc&filter.name=test

### W serwisie/repozytorium
```csharp
if (query.IsPagingSpecified)
    dbQuery = dbQuery.Skip((query.Page!.Value - 1) * query.Count!.Value).Take(query.Count.Value);

if (query.IsSortingSpecified)
{
    if (query.IsSortBy("Name"))
        dbQuery = query.SortDir == SortDirection.Asc ? dbQuery.OrderBy(x => x.Name) : dbQuery.OrderByDescending(x => x.Name);
}
```

### Zasady
- Count w IPagedResult = łączna liczba bez stronicowania (do kalkulacji stron w UI)
- SortDirection.Undefined = brak sortowania (domyślne)
- IsSortBy() sprawdza case-insensitive — używaj zawsze zamiast string.Equals ręcznie
- IsPagingSpecified = OBIE wartości Page i Count muszą być != null
```
