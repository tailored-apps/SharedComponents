using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Messages.Notifications;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.Services.Orders.Entities;
using TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.Services.Orders.Handlers;

/// <summary>
/// One handler for the notifications of every gateway: the provider decides what the notification means,
/// this handler applies it to the order and publishes <see cref="OrderPaid"/>.
/// </summary>
public class ReceivePaymentNotificationCommandHandler(IPaymentService payments, IOrderRepository orders, IPublisher publisher, IDateTimeProvider clock)
    : IReceivePaymentNotificationCommandHandler
{
    public async Task<ReceivePaymentNotificationResponse> Handle(ReceivePaymentNotification request, CancellationToken cancellationToken)
    {
        var result = await payments.HandleWebhookAsync(request.Provider, request.Notification, cancellationToken);

        switch (result)
        {
            case PaymentWebhookRejected rejected:
                // Nothing in a rejected notification can be trusted.
                return new ReceivePaymentNotificationResponse { Accepted = false, Rejection = rejected.Rejection.ToString() };

            case PaymentWebhookProcessed processed:
                await ApplyAsync(processed.Payment, cancellationToken);
                return Acknowledge(processed.Payment);

            case PaymentWebhookIgnored ignored:
                return Acknowledge(ignored.Payment);

            default:
                return new ReceivePaymentNotificationResponse { Accepted = true };
        }
    }

    private async Task ApplyAsync(PaymentResponse payment, CancellationToken cancellationToken)
    {
        // Depending on the gateway the notification carries our own reference (the order number) or the payment id.
        var order = payment.PaymentUniqueId is null ? null : await orders.FindByPaymentReferenceAsync(payment.PaymentUniqueId, cancellationToken);
        if (order is null || order.Status == nameof(OrderStatus.Paid))
            return;   // unknown payment, or a notification delivered twice

        if (payment.PaymentStatus == PaymentStatusEnum.Finished)
        {
            var paidAt = clock.OffsetUtcNow;
            await orders.MarkPaidAsync(order.Number, paidAt, cancellationToken);

            await publisher.Publish(new OrderPaid
            {
                Number = order.Number,
                CustomerEmail = order.CustomerEmail,
                Amount = order.Amount,
                Currency = order.Currency,
                PaidAt = paidAt,
            }, cancellationToken);
        }
        else if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
        {
            await orders.MarkPaymentRejectedAsync(order.Number, cancellationToken);
        }
    }

    /// <summary>Answers with the text the gateway expects (Tpay: TRUE, Adyen: [accepted]) when the provider names one.</summary>
    private static ReceivePaymentNotificationResponse Acknowledge(PaymentResponse? payment)
        => new() { Accepted = true, Acknowledgement = payment?.ResponseObject as string ?? "OK" };
}
