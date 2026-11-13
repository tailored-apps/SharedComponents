using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Orders.Handlers;

/// <summary>The handler of a request without a response returns a plain Task.</summary>
public class CancelOrderCommandHandler(IOrderRepository orders) : ICancelOrderCommandHandler
{
    public Task Handle(CancelOrder request, CancellationToken cancellationToken)
        => orders.CancelAsync(request.Number, cancellationToken);
}
