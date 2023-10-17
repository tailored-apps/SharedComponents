using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.MediatR.Email.Interfaces.Handlers.Commands;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Handlers
{
    public class SendMailCommandHandler : ISendMailCommandHandler
    {
        private readonly IEmailProvider emailService;
        private readonly IMailMessageBuilder mailMessageBuilder;
        public SendMailCommandHandler(IEmailProvider emailService, IMailMessageBuilder mailMessageBuilder)
        {
            this.emailService = emailService;
            this.mailMessageBuilder = mailMessageBuilder;
        }

        public async Task<SendMailResponse> Handle(SendMail request, CancellationToken cancellationToken)
        {
            
                var body = mailMessageBuilder.Build(request.Template, request.TemplateVariables, request.Templates);
                await emailService.SendMail(request.Recipent, request.Subject, body, request.Attachments);
                return new SendMailResponse() { };
            

        }
    }
}
