using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;

namespace TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;

public interface ICancelOrderCommandHandler : IRequestHandler<CancelOrder>;
