using MediatR;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Interfaces.Handlers.Commands
{
    /// <summary>
    /// Defines the MediatR request handler contract for the <see cref="SendMail"/> command.
    /// Implementations are responsible for building the email body and dispatching the message.
    /// </summary>
    public interface ISendMailCommandHandler : IRequestHandler<SendMail, SendMailResponse>
    {
    }
}
