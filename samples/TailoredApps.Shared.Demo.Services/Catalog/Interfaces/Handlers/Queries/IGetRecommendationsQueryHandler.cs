using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;

namespace TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;

public interface IGetRecommendationsQueryHandler : IRequestHandler<GetRecommendations, GetRecommendationsResponse>;
