using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Email
{
    /// <summary>
    /// Implementation of <see cref="IEmailProvider"/> that sends email messages via SMTP.
    /// Uses <see cref="SmtpEmailServiceOptions"/> for server configuration.
    /// </summary>
    public class SmtpEmailProvider : IEmailProvider
    {
        private readonly IOptions<SmtpEmailServiceOptions> options;

        /// <summary>
        /// Initializes a new instance of <see cref="SmtpEmailProvider"/> with the specified SMTP options.
        /// </summary>
        /// <param name="options">The SMTP configuration options wrapped in an <see cref="IOptions{TOptions}"/> accessor.</param>
        public SmtpEmailProvider(IOptions<SmtpEmailServiceOptions> options)
        {
            this.options = options;
        }

        /// <summary>
        /// Retrieves email messages from the mail server. This method is not yet implemented.
        /// </summary>
        /// <param name="folder">The mailbox folder to retrieve messages from.</param>
        /// <param name="sender">Filter by sender email address.</param>
        /// <param name="recipent">Filter by recipient email address.</param>
        /// <param name="fromLast">Time span to filter messages received within that period.</param>
        /// <returns>A task that retrieves a collection of <see cref="Models.MailMessage"/> objects.</returns>
        /// <exception cref="System.NotImplementedException">Always thrown; this method is not implemented.</exception>
        public async Task<ICollection<Models.MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Sends an email message via the configured SMTP server.
        /// In non-production environments the message is redirected to the configured catch-all address.
        /// </summary>
        /// <param name="recipnet">The intended recipient email address.</param>
        /// <param name="topic">The subject line of the email.</param>
        /// <param name="messageBody">The HTML body content of the email.</param>
        /// <param name="attachments">
        /// An optional dictionary of attachment file names mapped to their byte content.
        /// Pass <c>null</c> or an empty dictionary when no attachments are needed.
        /// </param>
        /// <returns>
        /// A task that resolves to the RFC 2822 Message-ID header value assigned to the sent message.
        /// </returns>
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
                mailMessage.IsBodyHtml = true;
                mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                var msgId = $"<{Guid.NewGuid().ToString().Replace(" - ", "")}@{mailMessage.Sender.Host}>";
                mailMessage.Headers.Add(new System.Collections.Specialized.NameValueCollection() { { "Message-ID", msgId } });
                await client.SendMailAsync(mailMessage);
                return msgId;
            }
        }
    }

    /// <summary>
    /// Provides extension methods for registering email provider implementations in the dependency injection container.
    /// </summary>
    public static class SmtpEmailProviderExtensions
    {
        /// <summary>
        /// Registers the <see cref="SmtpEmailProvider"/> and its required dependencies in the DI container.
        /// Options are loaded from the application configuration using <see cref="SmtpEmailConfigureOptions"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        public static void RegisterSmtpProvider(this IServiceCollection services)
        {
            services.AddOptions<SmtpEmailServiceOptions>();
            services.ConfigureOptions<SmtpEmailConfigureOptions>();
            services.AddTransient<IEmailProvider, SmtpEmailProvider>();
        }

        /// <summary>
        /// Registers the <see cref="EmailServiceToConsolleWritter"/> console provider and its required dependencies in the DI container.
        /// Options are loaded from the application configuration using <see cref="SmtpEmailConfigureOptions"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        public static void RegisterConsoleProvider(this IServiceCollection services)
        {
            services.AddOptions<SmtpEmailServiceOptions>();
            services.ConfigureOptions<SmtpEmailConfigureOptions>();
            services.AddTransient<IEmailProvider, EmailServiceToConsolleWritter>();
        }
    }

    /// <summary>
    /// Configures <see cref="SmtpEmailServiceOptions"/> by reading values from the application configuration.
    /// Implements <see cref="IConfigureOptions{TOptions}"/> to integrate with the options infrastructure.
    /// </summary>
    public class SmtpEmailConfigureOptions : IConfigureOptions<SmtpEmailServiceOptions>
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of <see cref="SmtpEmailConfigureOptions"/> with the given application configuration.
        /// </summary>
        /// <param name="configuration">The application configuration used to read SMTP settings.</param>
        public SmtpEmailConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Populates the provided <see cref="SmtpEmailServiceOptions"/> instance with values
        /// from the configuration section identified by <see cref="SmtpEmailServiceOptions.ConfigurationKey"/>.
        /// </summary>
        /// <param name="options">The options instance to configure.</param>
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
