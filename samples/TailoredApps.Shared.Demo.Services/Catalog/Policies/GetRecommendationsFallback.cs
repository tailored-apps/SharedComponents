using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;

namespace TailoredApps.Demo.Services.Catalog.Policies;

/// <summary>A product page without recommendations is better than no product page.</summary>
public class GetRecommendationsFallback : IFallbackHandler<GetRecommendations, GetRecommendationsResponse>
{
    public Task<GetRecommendationsResponse> HandleFallback(GetRecommendations request, CancellationToken cancellationToken)
        => Task.FromResult(new GetRecommendationsResponse());
}
