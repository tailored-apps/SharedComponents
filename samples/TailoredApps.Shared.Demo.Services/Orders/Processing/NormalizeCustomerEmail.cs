using ForkForward.Mediator.Pipeline;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;

namespace TailoredApps.Demo.Services.Orders.Processing;

/// <summary>
/// A pre-processor runs before the behaviors and the handler: the same customer is not stored under two
/// spellings of one address, and the validator sees the address the order will carry.
/// </summary>
public class NormalizeCustomerEmail : IRequestPreProcessor<CreateOrder>
{
    public Task Process(CreateOrder request, CancellationToken cancellationToken)
    {
        request.CustomerEmail = request.CustomerEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        return Task.CompletedTask;
    }
}
