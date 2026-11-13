using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Demo.DataModel.Catalog.Responses.Queries;

public class GetProductsResponse : IPagedResult<Product>
{
    public ICollection<Product> Results { get; set; } = [];

    public int Count { get; set; }
}
