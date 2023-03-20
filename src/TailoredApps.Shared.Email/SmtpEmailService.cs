using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace TailoredApps.Shared.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IOptions<SmtpEmailServiceOptions> options;
        public SmtpEmailService(IOptions<SmtpEmailServiceOptions> options)
        {
            this.options = options;
        }
        public void SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {

            using (var client = new SmtpClient(options.Value.Host, options.Value.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(options.Value.UserName, options.Value.Password);
                client.EnableSsl = options.Value.EnableSsl;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(options.Value.From),
                    Subject = topic
                };
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        mailMessage.Attachments.Add(new Attachment(new MemoryStream(attachment.Value), attachment.Key));
                    }
                }
                if (options.Value.IsProd)
                {
                    mailMessage.To.Add(recipnet);
                }
                else
                {
                    mailMessage.To.Add(options.Value.CatchAll);
                }
                mailMessage.Body = messageBody;
                mailMessage.IsBodyHtml = true;

                client.Send(mailMessage);
            }
        }
    }
}
