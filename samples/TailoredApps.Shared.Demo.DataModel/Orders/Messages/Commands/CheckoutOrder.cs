using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Commands;

/// <summary>Starts a payment for the order at the chosen gateway.</summary>
public class CheckoutOrder : IRequest<CheckoutOrderResponse>
{
    /// <summary>The order number; the controller takes it from the route.</summary>
    public string Number { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string? Channel { get; set; }
}
