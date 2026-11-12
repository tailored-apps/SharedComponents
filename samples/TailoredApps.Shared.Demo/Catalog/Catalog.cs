using FluentValidation;
using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Data;
using TailoredApps.Shared.EntityFramework.Querying;
using TailoredApps.Shared.MediatR.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PagedRequest;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Demo.Catalog;

public record ProductDto(int Id, string Name, string Category, decimal Price);

// ─── A page of products: Querying + MediatR.PagedRequest + the EntityFramework paging and sorting ───────────

public class ProductFilter : QueryBase
{
    public string? NameContains { get; set; }

    public string? Category { get; set; }
}

public class GetProducts : PagedAndSortedRequest<PagedResult<ProductDto>, ProductFilter, ProductDto>
{
    /// <summary>The only properties a client may sort by.</summary>
    public static readonly string[] SortableFields = ["Name", "Price", "Category"];

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    /// <summary>
    /// Without paging the query extensions return every row, and without sorting the pages have no stable
    /// order. A request that leaves them out gets the first page sorted by name; the query string overrides it.
    /// </summary>
    public GetProducts()
    {
        Page = 1;
        Count = DefaultPageSize;
        SortField = "Name";
        SortDir = SortDirection.Asc;
    }
}

/// <summary>
/// Rejects paging and sorting parameters before the query runs, so that a client mistake is a 400 with the
/// field named, not an exception from the query extensions.
/// </summary>
public class GetProductsValidator : AbstractValidator<GetProducts>
{
    public GetProductsValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).When(r => r.Page.HasValue);
        RuleFor(r => r.Count).InclusiveBetween(1, GetProducts.MaxPageSize).When(r => r.Count.HasValue);
        RuleFor(r => r.SortField)
            .Must(field => GetProducts.SortableFields.Contains(field, StringComparer.OrdinalIgnoreCase))
            .When(r => !string.IsNullOrEmpty(r.SortField))
            .WithMessage($"Products can be sorted by {string.Join(", ", GetProducts.SortableFields)}.");
    }
}

public class GetProductsHandler(ShopDbContext db) : IRequestHandler<GetProducts, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(GetProducts request, CancellationToken cancellationToken)
    {
        IQueryable<Product> products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Filter?.NameContains))
            products = products.Where(p => p.Name.Contains(request.Filter.NameContains));
        if (!string.IsNullOrWhiteSpace(request.Filter?.Category))
            products = products.Where(p => p.Category == request.Filter.Category);

        // The validator has already checked the parameters; the allow-list and the page-size limit of the
        // query extensions keep the query safe on their own.
        var page = await products
            .ApplySorting(request, GetProducts.SortableFields)
            .PagingAsync(request);

        return page.Project(p => new ProductDto(p.Id, p.Name, p.Category, p.Price));
    }
}

// ─── One product: validation, caching (MediatR + MediatR.Caching) ───────────────────────────────────────────

public record GetProduct(int Id) : ICachableRequest<ProductDto>
{
    public string GetCacheKey() => $"product:{Id}";
}

public class GetProductValidator : AbstractValidator<GetProduct>
{
    public GetProductValidator() => RuleFor(r => r.Id).GreaterThan(0);
}

public class GetProductCachePolicy : ICachePolicy<GetProduct, ProductDto>
{
    public TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(10);

    public IReadOnlyCollection<string>? Tags => ["products"];

    public string GetCacheKey(GetProduct request) => request.GetCacheKey();
}

/// <summary>Counts handler runs, so that the demo (and its tests) can show a cached response skipping the handler.</summary>
public class HandlerCallCounter
{
    private int getProduct;

    public int GetProduct => getProduct;

    public void CountGetProduct() => Interlocked.Increment(ref getProduct);
}

public class GetProductHandler(ShopDbContext db, HandlerCallCounter counter) : IRequestHandler<GetProduct, ProductDto>
{
    public async Task<ProductDto> Handle(GetProduct request, CancellationToken cancellationToken)
    {
        counter.CountGetProduct();
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.Id} does not exist.");
        return new ProductDto(product.Id, product.Name, product.Category, product.Price);
    }
}

// ─── Recommendations from an unreliable source: retry and fallback ──────────────────────────────────────────

public record GetRecommendations(int ProductId) : IRequest<IReadOnlyList<string>>;

/// <summary>Stands in for a remote service. The first calls fail, as many as <see cref="FailuresLeft"/> says.</summary>
public class RecommendationSource
{
    public int FailuresLeft { get; set; }

    public int Calls { get; private set; }

    public IReadOnlyList<string> For(int productId)
    {
        Calls++;
        if (FailuresLeft > 0)
        {
            FailuresLeft--;
            throw new TimeoutException("The recommendation service did not answer.");
        }

        return [$"Customers who bought product {productId} also bought a teapot."];
    }
}

public class GetRecommendationsHandler(RecommendationSource source) : IRequestHandler<GetRecommendations, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetRecommendations request, CancellationToken cancellationToken)
        => Task.FromResult(source.For(request.ProductId));
}

public class GetRecommendationsRetry : IRetryableRequest<GetRecommendations, IReadOnlyList<string>>
{
    public int RetryAttempts => 2;

    public int RetryDelay => 10;

    public int ExceptionsAllowedBeforeCircuitTrip => 100;
}

public class GetRecommendationsFallback : IFallbackHandler<GetRecommendations, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleFallback(GetRecommendations request, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);
}

// ─── HTTP ───────────────────────────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/products")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>GET /api/products?page=1&amp;count=3&amp;sortField=Price&amp;sortDir=Desc&amp;filter.category=tea</summary>
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] GetProducts request, CancellationToken cancellationToken)
        => mediator.Send(request, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ProductDto> Get(int id, CancellationToken cancellationToken)
        => mediator.Send(new GetProduct(id), cancellationToken);

    [HttpGet("{id:int}/recommendations")]
    public Task<IReadOnlyList<string>> Recommendations(int id, CancellationToken cancellationToken)
        => mediator.Send(new GetRecommendations(id), cancellationToken);
}
