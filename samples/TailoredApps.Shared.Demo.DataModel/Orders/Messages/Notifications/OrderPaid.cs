using ForkForward.Mediator;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Notifications;

/// <summary>
/// Published when a gateway confirms a payment. A notification has any number of handlers and none of them
/// answers: here one mails the customer and one records the sale.
/// </summary>
public class OrderPaid : INotification
{
    public string Number { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset PaidAt { get; set; }
}
