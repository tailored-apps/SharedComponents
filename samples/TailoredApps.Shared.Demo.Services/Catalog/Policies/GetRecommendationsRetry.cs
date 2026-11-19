using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Shared.Mediator.Interfaces.Messages;

namespace TailoredApps.Demo.Services.Catalog.Policies;

public class GetRecommendationsRetry : IRetryableRequest<GetRecommendations, GetRecommendationsResponse>
{
    public int RetryAttempts => 2;

    public int RetryDelay => 10;

    public int ExceptionsAllowedBeforeCircuitTrip => 100;
}
