using ForkForward.Mediator.Pipeline;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Processing;

/// <summary>A post-processor runs after the handler succeeded and sees its response.</summary>
public class CountStartedPayments(ShopMetrics metrics) : IRequestPostProcessor<CheckoutOrder, CheckoutOrderResponse>
{
    public Task Process(CheckoutOrder request, CheckoutOrderResponse response, CancellationToken cancellationToken)
    {
        metrics.PaymentStarted(request.Provider);
        return Task.CompletedTask;
    }
}
