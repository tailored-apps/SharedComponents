using TailoredApps.Demo.DataModel.Orders.Model;

namespace TailoredApps.Demo.DataModel.Orders.Responses.Queries;

public class GetOrderResponse
{
    public Order Order { get; set; } = new();
}
