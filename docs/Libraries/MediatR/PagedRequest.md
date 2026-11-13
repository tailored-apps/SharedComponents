# TailoredApps.Shared.MediatR.PagedRequest

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.PagedRequest)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.PagedRequest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Biblioteka dostarcza bazową klasę `PagedAndSortedRequest<TResponse, TQuery, TModel>` dla requestów MediatR, które wymagają paginacji i sortowania. Standaryzuje parametry stronicowania (`Page`, `Count`) i sortowania (`SortField`, `SortDir`) we wszystkich zapytaniach listowych aplikacji.

Klasa jest ściśle zintegrowana z biblioteką `TailoredApps.Shared.Querying` — wymaga, aby `TQuery` dziedziczyło po `QueryBase`, a `TResponse` implementowało `IPagedResult<TModel>`.

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

Handler korzysta z `ApplySorting` i `PagingAsync` z pakietu `TailoredApps.Shared.EntityFramework`. Obie metody przyjmują request bezpośrednio (implementuje on `ISortingParameters` i `IPagingParameters`) i walidują wartości przysłane przez klienta.

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

        // Aplikacja filtrów
        if (!string.IsNullOrWhiteSpace(request.Filter?.NameContains))
            query = query.Where(p => p.Name.Contains(request.Filter.NameContains));

        if (request.Filter?.MinPrice.HasValue == true)
            query = query.Where(p => p.Price >= request.Filter.MinPrice.Value);

        // Sortowanie: SortField pochodzi od klienta, więc jest sprawdzane względem listy dozwolonych pól.
        // Paginacja: waliduje Page i Count, liczy wszystkie pasujące wiersze i odczytuje jedną stronę
        // (cały wynik, gdy request nie zawiera wartości stronicowania).
        var page = await query
            .ApplySorting(request, allowedSortFields: new[] { "Name", "Price" })
            .PagingAsync(request);

        return new ProductListResponse
        {
            Results = page.Results.Select(p => p.ToDto()).ToList(),
            Count = page.Count   // łączna liczba pasujących wierszy
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

!!! info "Trimming i NativeAOT"
    Typy tego pakietu (`PagedAndSortedRequest<,,>`, `IPagedResult<>`) są zgodne z trimmingiem i NativeAOT; handler rejestruje się wtedy
    po nazwie — zob. [MediatR: Trimming i NativeAOT](index.md#trimming-i-nativeaot). Rozszerzenia stronicowania i sortowania użyte
    w przykładzie pochodzą z `TailoredApps.Shared.EntityFramework`, która z NativeAOT zgodna nie jest (EF Core).

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
| `IsSortingSpecified` | Właściwość `bool` | `true` gdy SortField nie jest pusty, a SortDir ma wartość (także dla `SortDirection.Undefined`) |
| `Filter` | Właściwość `TQuery` | Obiekt filtra dziedziczący po `QueryBase` |
| `IsSortBy(string)` | Metoda | Sprawdza czy sortowanie jest po danym polu (case-insensitive) |

---

## 🔒 Bezpieczeństwo

`Page`, `Count` i `SortField` pochodzą wprost z żądania HTTP. Warstwa EF (`PagingQuery<T>`, `ApplySorting`) egzekwuje limity: `Page >= 1`, `1 <= Count <= PagingQuery<T>.MaxPageSize` (domyślnie 1000) oraz pojedynczą nazwę właściwości jako pole sortowania; naruszenie kończy się `ArgumentException`/`ArgumentOutOfRangeException`. Dla encji z wrażliwymi kolumnami przekaż do `ApplySorting` listę dozwolonych pól.

---

## 🤖 AI Agent Prompt

````markdown
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
// using TailoredApps.Shared.EntityFramework.Querying;  (pakiet TailoredApps.Shared.EntityFramework)
var page = await query
    .ApplySorting(request, allowedSortFields: new[] { "Name" })   // waliduje SortField
    .PagingAsync(request);                                        // waliduje Page/Count, zwraca PagedResult<T>
// page.Results — elementy strony, page.Count — łączna liczba
```

### Zasady
- TQuery musi dziedziczyć po QueryBase
- TResponse musi implementować IPagedResult<TModel>
- IsPagingSpecified = Page i Count mają wartość — PagingAsync stronicuje tylko wtedy; jeśli sam wywołujesz Skip/Take, sprawdź to i ogranicz Count
- IsSortBy("Name") — sprawdza case-insensitive
- Nie przekazuj SortField do Dynamic LINQ samodzielnie — używaj ApplySorting z listą dozwolonych pól
- Aplikacja z PublishAot: typy pakietu działają, handler rejestruj przez cfg.AddRequestHandler<,,>(); rozszerzenia z TailoredApps.Shared.EntityFramework (EF Core) nie są zgodne z NativeAOT
````
