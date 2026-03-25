using System.Collections.Generic;
using MediatR;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Messages.Commands
{
    /// <summary>
    /// MediatR command that triggers sending an email message.
    /// Carries all data required by the handler to compose and dispatch the email.
    /// </summary>
    public class SendMail : IRequest<SendMailResponse>
    {
        /// <summary>
        /// Gets or sets the email subject line.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the recipient's email address.
        /// </summary>
        public string Recipent { get; set; }

        /// <summary>
        /// Gets or sets the name or path of the template to render as the email body.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of file names to binary content for email attachments.
        /// </summary>
        public Dictionary<string, byte[]> Attachments { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of placeholder keys and their values used when rendering the template.
        /// </summary>
        public Dictionary<string, string> TemplateVariables { get; set; }

        /// <summary>
        /// Gets or sets an optional dictionary of named templates passed to the mail message builder.
        /// </summary>
        public Dictionary<string, string> Templates { get; set; }
    }
}
