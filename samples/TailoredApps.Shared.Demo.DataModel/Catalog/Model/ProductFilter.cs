using TailoredApps.Shared.Querying;

namespace TailoredApps.Demo.DataModel.Catalog.Model;

/// <summary>The filter of a product list: <c>?filter.category=tea&amp;filter.nameContains=green</c>.</summary>
public class ProductFilter : QueryBase
{
    public string? NameContains { get; set; }

    public string? Category { get; set; }
}
