using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Shared.Email.Office365
{
    /// <summary>
    /// Email provider implementation for Office 365 using IMAP with OAuth2 (client-credentials flow).
    /// Authenticates against Azure AD as a confidential client application.
    /// </summary>
    public class Office365EmailProvider : IEmailProvider
    {
        private readonly IOptions<AuthenticationConfig> options;
        private readonly string[] scopes = new string[] {
                 "https://outlook.office365.com/.default"

            };
        private readonly IConfidentialClientApplication confidentialClientApplication;
        /// <summary>
        /// Initializes a new instance of <see cref="Office365EmailProvider"/> and builds
        /// the confidential client application using either a client secret or a certificate.
        /// </summary>
        /// <param name="options">The Office 365 authentication configuration options.</param>
        public Office365EmailProvider(IOptions<AuthenticationConfig> options)
        {
            this.options = options;

            // You can run this sample using ClientSecret or Certificate. The code will differ only when instantiating the IConfidentialClientApplication
            bool isUsingClientSecret = IsAppUsingClientSecret(options.Value);

            // Even if this is a console application here, a daemon application is a confidential client application
            IConfidentialClientApplication app;

            if (isUsingClientSecret)
            {
                // Even if this is a console application here, a daemon application is a confidential client application
                app = ConfidentialClientApplicationBuilder.Create(options.Value.ClientId)
                    .WithClientSecret(options.Value.ClientSecret)
                    .WithAuthority(new Uri(options.Value.Authority))
                    .Build();
            }

            else
            {
                ICertificateLoader certificateLoader = new DefaultCertificateLoader();
                certificateLoader.LoadIfNeeded(options.Value.Certificate);

                app = ConfidentialClientApplicationBuilder.Create(options.Value.ClientId)
                    .WithCertificate(options.Value.Certificate.Certificate)
                    .WithAuthority(new Uri(options.Value.Authority))
                    .Build();
            }

            app.AddInMemoryTokenCache();

            confidentialClientApplication = app;
        }


        /// <summary>
        /// Checks if the sample is configured for using ClientSecret or Certificate. This method is just for the sake of this sample.
        /// You won't need this verification in your production application since you will be authenticating in AAD using one mechanism only.
        /// </summary>
        /// <param name="config">Configuration from appsettings.json</param>
        /// <returns></returns>
        private static bool IsAppUsingClientSecret(AuthenticationConfig config)
        {
            string clientSecretPlaceholderValue = "[Enter here a client secret for your application]";

            if (!String.IsNullOrWhiteSpace(config.ClientSecret) && config.ClientSecret != clientSecretPlaceholderValue)
            {
                return true;
            }

            else if (config.Certificate != null)
            {
                return false;
            }

            else
                throw new Exception("You must choose between using client secret or certificate. Please update appsettings.json file.");
        }
        /// <summary>
        /// Retrieves e-mail messages from the configured Office 365 mailbox using IMAP with OAuth2.
        /// </summary>
        /// <param name="folderName">
        /// The name of the IMAP sub-folder to search in. Defaults to the inbox when empty.
        /// </param>
        /// <param name="sender">Optional filter: only messages whose From address contains this value are returned.</param>
        /// <param name="recipent">Optional filter: only messages whose To address contains this value are returned.</param>
        /// <param name="fromLast">
        /// Optional time-span filter: only messages delivered within the last <paramref name="fromLast"/> are returned.
        /// </param>
        /// <returns>A collection of <see cref="Models.MailMessage"/> objects matching the specified criteria.</returns>
        public async Task<ICollection<Models.MailMessage>> GetMail(string folderName = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            var response = new List<Models.MailMessage>();
            var authToken = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
            var oauth2 = new SaslMechanismOAuth2(options.Value.MailBox, authToken.AccessToken);

            using (var client = new ImapClient())
            {
                await client.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(oauth2);
                var folder = client.Inbox;
                if (!string.IsNullOrEmpty(folderName))
                {
                    folder = folder.GetSubfolder(folderName);
                }
                folder.Open(FolderAccess.ReadOnly);

                var query = SearchQuery.All;
                if (!string.IsNullOrEmpty(sender))
                {
                    query = query.And(SearchQuery.FromContains(sender));
                }
                if (!string.IsNullOrEmpty(recipent))
                {
                    query = query.And(SearchQuery.ToContains(recipent));
                }
                if (fromLast.HasValue)
                {
                    query = query.And(SearchQuery.DeliveredAfter(DateTime.Now - fromLast.Value));
                }

                var uids = folder.Search(query);
                var items = folder.Fetch(uids, MessageSummaryItems.UniqueId | MessageSummaryItems.Headers);

                foreach (var uid in uids)
                {
                    var message = folder.GetMessage(uid);

                    var mailMessage = new Models.MailMessage
                    {
                        HtmlBody = message.HtmlBody,
                        Topic = message.Subject,
                        Copy = string.Join(",", message.Cc?.Select(z => z.ToString())),
                        Sender = string.Join(",", message.From?.Select(z => z.ToString())),
                        Recipent = string.Join(",", message.To?.Select(z => z.ToString())),
                        Body = message.GetTextBody(MimeKit.Text.TextFormat.Plain),
                        Date = message.Date,
                        Attachements = GetAttachements(message.Attachments)
                    };
                    response.Add(mailMessage);

                }
                await client.DisconnectAsync(true);
            }
            return response;
        }

        private Dictionary<string, string> GetAttachements(IEnumerable<MimeEntity> attachments)
        {
            var result = new Dictionary<string, string>();
            foreach (var attachement in attachments)
            {
                if (attachement is MimePart)
                {

                    var res = attachement as MimePart;
                    byte[] bytes;
                    using (var memoryStream = new MemoryStream())
                    {
                        res.Content.Stream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                    result.Add(res.ContentType.ToString(), Convert.ToBase64String(bytes));
                }
            }
            return result;
        }

        /// <summary>Sends an e-mail message via Office 365.</summary>
        /// <param name="recipnet">The recipient e-mail address.</param>
        /// <param name="topic">The subject of the message.</param>
        /// <param name="messageBody">The body content of the message.</param>
        /// <param name="attachments">A dictionary of attachment file names mapped to their byte content.</param>
        /// <returns>A string result or identifier for the sent message.</returns>
        public async Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>Extension methods for registering the Office 365 email provider with the DI container.</summary>
    public static class Office365EmailProviderExtensions
    {
        /// <summary>
        /// Registers <see cref="Office365EmailProvider"/> and its configuration options with the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        public static void RegisterOffice365Provider(this IServiceCollection services)
        {
            services.AddOptions<AuthenticationConfig>();
            services.ConfigureOptions<Office365EmailConfigureOptions>();
            services.AddTransient<IEmailProvider, Office365EmailProvider>();
        }
    }

    /// <summary>
    /// Configures <see cref="AuthenticationConfig"/> options by binding values from the application configuration.
    /// </summary>
    public class Office365EmailConfigureOptions : IConfigureOptions<AuthenticationConfig>
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of <see cref="Office365EmailConfigureOptions"/>.
        /// </summary>
        /// <param name="configuration">The application configuration used to read Office 365 settings.</param>
        public Office365EmailConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>Populates <paramref name="options"/> with values from the application configuration section.</summary>
        /// <param name="options">The <see cref="AuthenticationConfig"/> instance to configure.</param>
        public void Configure(AuthenticationConfig options)
        {
            var section = configuration.GetSection(AuthenticationConfig.ConfigurationKey).Get<AuthenticationConfig>();

            options.Instance = section.Instance;
            options.ApiUrl = section.ApiUrl;
            options.Tenant = section.Tenant;
            options.ClientId = section.ClientId;
            options.MailBox = section.MailBox;
            options.ClientSecret = section.ClientSecret;
            options.Certificate = section.Certificate;
        }
    }
}
