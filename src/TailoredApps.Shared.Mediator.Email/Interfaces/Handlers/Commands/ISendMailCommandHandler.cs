using ForkForward.Mediator;
using TailoredApps.Shared.Mediator.Email.Messages.Commands;
using TailoredApps.Shared.Mediator.Email.Responses;

namespace TailoredApps.Shared.Mediator.Email.Interfaces.Handlers.Commands;

/// <summary>
/// Defines the mediator request handler contract for the <see cref="SendMail"/> command.
/// Implementations are responsible for building the email body and dispatching the message.
/// </summary>
public interface ISendMailCommandHandler : IRequestHandler<SendMail, SendMailResponse>
{
}
