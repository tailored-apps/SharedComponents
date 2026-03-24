using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.MediatR.Email.Interfaces.Handlers.Commands;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Handlers
{
    /// <summary>
    /// MediatR request handler that processes a <see cref="SendMail"/> command by building
    /// the email body from a template and dispatching the message via the configured
    /// <see cref="IEmailProvider"/>.
    /// </summary>
    public class SendMailCommandHandler : ISendMailCommandHandler
    {
        private readonly IEmailProvider emailService;
        private readonly IMailMessageBuilder mailMessageBuilder;

        /// <summary>
        /// Initializes a new instance of <see cref="SendMailCommandHandler"/>.
        /// </summary>
        /// <param name="emailService">The email provider used to send messages.</param>
        /// <param name="mailMessageBuilder">The builder used to render the email body from a template.</param>
        public SendMailCommandHandler(IEmailProvider emailService, IMailMessageBuilder mailMessageBuilder)
        {
            this.emailService = emailService;
            this.mailMessageBuilder = mailMessageBuilder;
        }

        /// <summary>
        /// Handles the <see cref="SendMail"/> command: renders the email body and sends the message.
        /// </summary>
        /// <param name="request">The send-mail command containing recipient, subject, template, and attachments.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="SendMailResponse"/> containing the provider-assigned message identifier.
        /// </returns>
        public async Task<SendMailResponse> Handle(SendMail request, CancellationToken cancellationToken)
        {

            var body = mailMessageBuilder.Build(request.Template, request.TemplateVariables, request.Templates);
            var messageId = await emailService.SendMail(request.Recipent, request.Subject, body, request.Attachments);
            return new SendMailResponse() { MessageId= messageId };


        }
    }
}
