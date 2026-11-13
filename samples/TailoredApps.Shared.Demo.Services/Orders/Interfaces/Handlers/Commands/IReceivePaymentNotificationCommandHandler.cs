using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;

namespace TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;

public interface IReceivePaymentNotificationCommandHandler : IRequestHandler<ReceivePaymentNotification, ReceivePaymentNotificationResponse>;
