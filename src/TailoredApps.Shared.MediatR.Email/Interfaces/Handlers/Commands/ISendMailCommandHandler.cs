using MediatR;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Interfaces.Handlers.Commands
{
    public interface ISendMailCommandHandler : IRequestHandler<SendMail, SendMailResponse>
    {
    }
}
