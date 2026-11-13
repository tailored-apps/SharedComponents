using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Messages.Queries;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;

namespace TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Queries;

public interface IGetOrderQueryHandler : IRequestHandler<GetOrder, GetOrderResponse>;
