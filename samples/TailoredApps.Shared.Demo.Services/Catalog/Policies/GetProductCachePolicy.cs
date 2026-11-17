using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

namespace TailoredApps.Demo.Services.Catalog.Policies;

/// <summary>Having this policy is what makes the caching behavior cache the request.</summary>
public class GetProductCachePolicy : ICachePolicy<GetProduct, GetProductResponse>
{
    public TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(10);

    public IReadOnlyCollection<string>? Tags => ["products"];

    public string GetCacheKey(GetProduct request) => request.GetCacheKey();
}
