using ForkForward.Mediator;
using Microsoft.Extensions.Logging;
using TailoredApps.Demo.DataModel.Orders.Messages.Notifications;
using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Notifications;

/// <summary>
/// The other handler of <see cref="OrderPaid"/>: counts the sale. It knows nothing about the mail. A handler
/// with nothing to await derives from <see cref="NotificationHandler{TNotification}"/> and stays synchronous.
/// </summary>
public class RecordOrderPaid(ShopMetrics metrics, ILogger<RecordOrderPaid> logger) : NotificationHandler<OrderPaid>
{
    protected override void Handle(OrderPaid notification)
    {
        metrics.OrderPaid();
        logger.LogInformation("Order {Number} paid at {PaidAt:O}", notification.Number, notification.PaidAt);
    }
}
