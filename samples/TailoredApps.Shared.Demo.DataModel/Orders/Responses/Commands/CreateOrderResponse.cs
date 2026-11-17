using TailoredApps.Demo.DataModel.Orders.Model;

namespace TailoredApps.Demo.DataModel.Orders.Responses.Commands;

public class CreateOrderResponse
{
    public Order Order { get; set; } = new();
}
