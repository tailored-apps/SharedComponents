using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.DataModel.Orders.Messages.Commands;

/// <summary>A notification (webhook) of a payment gateway, exactly as it arrived.</summary>
public class ReceivePaymentNotification : IRequest<ReceivePaymentNotificationResponse>
{
    public string Provider { get; set; } = string.Empty;

    public PaymentWebhookRequest Notification { get; set; } = new();
}
