using MediatR;
using System.Collections.Generic;
using TailoredApps.Shared.MediatR.Email.Responses;

namespace TailoredApps.Shared.MediatR.Email.Messages.Commands
{
    public class SendMail : IRequest<SendMailResponse>
    {
        public string Subject { get; set; }
        public string Recipent { get; set; }
        public string Template { get; set; }
        public Dictionary<string, byte[]> Attachments { get; set; }
        public Dictionary<string, string> TemplateVariables { get; set; }
        public Dictionary<string, string> Templates { get; set; }
    }
}
