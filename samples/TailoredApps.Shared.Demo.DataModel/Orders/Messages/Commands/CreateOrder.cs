using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Commands;

public class CreateOrder : IRequest<CreateOrderResponse>
{
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>The products of the order; an id given twice is two pieces of that product.</summary>
    public int[] ProductIds { get; set; } = [];
}
