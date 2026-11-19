using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

/// <summary>Recommendations come from an unreliable source: the request has a retry policy and a fallback.</summary>
public class GetRecommendations : IRequest<GetRecommendationsResponse>
{
    public int ProductId { get; set; }
}
