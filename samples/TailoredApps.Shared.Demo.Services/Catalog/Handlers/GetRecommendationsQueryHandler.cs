using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.Services.Catalog.Infrastructure;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

/// <summary>Throws when the source does: the retry behavior calls it again, and the fallback answers if that does not help.</summary>
public class GetRecommendationsQueryHandler(RecommendationSource source) : IGetRecommendationsQueryHandler
{
    public Task<GetRecommendationsResponse> Handle(GetRecommendations request, CancellationToken cancellationToken)
        => Task.FromResult(new GetRecommendationsResponse { Recommendations = source.For(request.ProductId) });
}
