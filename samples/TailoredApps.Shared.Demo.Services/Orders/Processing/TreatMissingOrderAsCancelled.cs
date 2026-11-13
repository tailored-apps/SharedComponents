using ForkForward.Mediator;
using ForkForward.Mediator.Pipeline;
using Microsoft.Extensions.Logging;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.Services.Orders.Exceptions;

namespace TailoredApps.Demo.Services.Orders.Processing;

/// <summary>
/// An exception handler replaces the exception of one request type with a response - unlike an exception
/// action, which only observes. Cancelling is idempotent for the caller: an order that does not exist is not
/// there to be paid either, so a repeated or late cancellation succeeds instead of failing with 404.
/// </summary>
public class TreatMissingOrderAsCancelled(ILogger<TreatMissingOrderAsCancelled> logger) : IRequestExceptionHandler<CancelOrder, Unit, OrderNotFoundException>
{
    public Task Handle(CancelOrder request, OrderNotFoundException exception, RequestExceptionHandlerState<Unit> state, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order {Number} to cancel does not exist; nothing to do", request.Number);
        state.SetHandled(Unit.Value);
        return Task.CompletedTask;
    }
}
