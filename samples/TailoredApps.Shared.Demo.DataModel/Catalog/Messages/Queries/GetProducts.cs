using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Shared.Mediator.PagedRequest;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

/// <summary>A page of products: Querying + Mediator.PagedRequest.</summary>
public class GetProducts : PagedAndSortedRequest<GetProductsResponse, ProductFilter, Product>
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
