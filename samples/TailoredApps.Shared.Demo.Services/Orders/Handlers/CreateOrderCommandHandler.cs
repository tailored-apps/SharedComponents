using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;
using TailoredApps.Shared.DateTime;

namespace TailoredApps.Demo.Services.Orders.Handlers;

public class CreateOrderCommandHandler(IOrderRepository orders, IDateTimeProvider clock) : ICreateOrderCommandHandler
{
    public async Task<CreateOrderResponse> Handle(CreateOrder request, CancellationToken cancellationToken)
    {
        var now = clock.OffsetUtcNow;
        var number = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21];

        return new CreateOrderResponse
        {
            Order = await orders.CreateAsync(number, request.CustomerEmail, request.ProductIds, now, cancellationToken),
        };
    }
}
