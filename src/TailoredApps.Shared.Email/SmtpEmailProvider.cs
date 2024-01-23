using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Email
{
    public class SmtpEmailProvider : IEmailProvider
    {
        private readonly IOptions<SmtpEmailServiceOptions> options;
        public SmtpEmailProvider(IOptions<SmtpEmailServiceOptions> options)
        {
            this.options = options;
        }

        public async Task<ICollection<Models.MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {

            using (var client = new SmtpClient(options.Value.Host, options.Value.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(options.Value.UserName, options.Value.Password);
                client.EnableSsl = options.Value.EnableSsl;
                client.Port = options.Value.Port;
                
                var mailMessage = new MailMessage
                {
                    Sender = new MailAddress(options.Value.From),
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
                mailMessage.IsBodyHtml = false;
                mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                var msgId= $"<{Guid.NewGuid().ToString().Replace(" - ","")}@{mailMessage.Sender.Host}>";
                mailMessage.Headers.Add(new System.Collections.Specialized.NameValueCollection() { { "Message-ID", msgId } });
                client.SendAsync(mailMessage,null);
                return msgId;
            }
        }
    }

    public static class SmtpEmailProviderExtensions
    {
        public static void RegisterSmtpProvider(this IServiceCollection services)
        {
            services.AddOptions<SmtpEmailServiceOptions>();
            services.ConfigureOptions<SmtpEmailConfigureOptions>();
            services.AddTransient<IEmailProvider, SmtpEmailProvider>();
        }
        public static void RegisterConsoleProvider(this IServiceCollection services)
        {
            services.AddOptions<SmtpEmailServiceOptions>();
            services.ConfigureOptions<SmtpEmailConfigureOptions>();
            services.AddTransient<IEmailProvider, EmailServiceToConsolleWritter>();
        }
    }



    public class SmtpEmailConfigureOptions : IConfigureOptions<SmtpEmailServiceOptions>
    {
        private readonly IConfiguration configuration;
        public SmtpEmailConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void Configure(SmtpEmailServiceOptions options)
        {
            var section = configuration.GetSection(SmtpEmailServiceOptions.ConfigurationKey).Get<SmtpEmailServiceOptions>();

            options.Host = section.Host;
            options.Port = section.Port;
            options.Password = section.Password;
            options.EnableSsl = section.EnableSsl;
            options.UserName = section.UserName;
            options.From = section.From; 
            options.IsProd = section.IsProd;
            options.CatchAll = section.CatchAll;
    }
    }
}
