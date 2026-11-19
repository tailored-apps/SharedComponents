using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Shared.Mediator.Caching;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

/// <summary>One product. The request names its own cache key (Mediator.Caching); the cache policy in the Services project uses it.</summary>
public class GetProduct : ICachableRequest<GetProductResponse>
{
    public int Id { get; set; }

    public string GetCacheKey() => $"product:{Id}";
}
