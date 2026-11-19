using ForkForward.Mediator;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Commands;

/// <summary>A request without a response: it either happens or throws.</summary>
public class CancelOrder : IRequest
{
    public string Number { get; set; } = string.Empty;
}
