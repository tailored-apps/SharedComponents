using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Queries;

public class GetOrder : IRequest<GetOrderResponse>
{
    public string Number { get; set; } = string.Empty;
}
