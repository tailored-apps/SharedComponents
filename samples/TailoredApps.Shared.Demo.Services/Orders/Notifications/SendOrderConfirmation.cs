using System.Globalization;
using ForkForward.Mediator;
using Microsoft.Extensions.Logging;
using TailoredApps.Demo.DataModel.Orders.Messages.Notifications;
using TailoredApps.Shared.Mediator.Email.Messages.Commands;

namespace TailoredApps.Demo.Services.Orders.Notifications;

/// <summary>
/// One of the two handlers of <see cref="OrderPaid"/>: mails the customer (Mediator.Email).
/// The default publisher runs the handlers one after another and an exception from one of them reaches the
/// code that published - here the handler of the gateway's notification. A mail server that is down must
/// not turn a confirmed payment into a failed request, so this handler logs its failure and lets it end here.
/// </summary>
public class SendOrderConfirmation(ISender sender, ILogger<SendOrderConfirmation> logger) : INotificationHandler<OrderPaid>
{
    public async Task Handle(OrderPaid notification, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new SendMail
            {
                Recipent = notification.CustomerEmail,
                Subject = $"Order {notification.Number} is paid",
                Template = "order-confirmation.html",
                TemplateVariables = new Dictionary<string, string>
                {
                    ["OrderNumber"] = notification.Number,
                    // The template is English; the server's culture must not decide how the customer sees the amount.
                    ["Amount"] = string.Create(CultureInfo.InvariantCulture, $"{notification.Amount:0.00} {notification.Currency}"),
                },
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The confirmation of order {Number} was not sent", notification.Number);
        }
    }
}
