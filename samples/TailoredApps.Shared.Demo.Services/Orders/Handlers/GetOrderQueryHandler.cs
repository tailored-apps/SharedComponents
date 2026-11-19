using TailoredApps.Demo.DataModel.Orders.Messages.Queries;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;
using TailoredApps.Demo.Services.Orders.Exceptions;
using TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Queries;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Orders.Handlers;

public class GetOrderQueryHandler(IOrderRepository orders) : IGetOrderQueryHandler
{
    public async Task<GetOrderResponse> Handle(GetOrder request, CancellationToken cancellationToken)
        => new() { Order = await orders.FindAsync(request.Number, cancellationToken) ?? throw new OrderNotFoundException(request.Number) };
}
