using ForkForward.Mediator.Pipeline;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Demo.Services.Orders.Exceptions;

namespace TailoredApps.Demo.Services.Orders.Processing;

/// <summary>
/// An exception action observes a failure of one request type without handling it: the exception goes on to
/// the caller (here it becomes the 503 response), and the outage is counted on the way.
/// </summary>
public class CountGatewayOutages(ShopMetrics metrics) : IRequestExceptionAction<CheckoutOrder, PaymentGatewayUnavailableException>
{
    public Task Execute(CheckoutOrder request, PaymentGatewayUnavailableException exception, CancellationToken cancellationToken)
    {
        metrics.GatewayOutage(exception.Provider);
        return Task.CompletedTask;
    }
}
