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
    public class Office365EmailProvider : IEmailProvider
    {
        private readonly IOptions<AuthenticationConfig> options;
        private readonly string[] scopes = new string[] {
                 "https://outlook.office365.com/.default"

            };
        private readonly IConfidentialClientApplication confidentialClientApplication;
        public Office365EmailProvider(IOptions<AuthenticationConfig> options)
        {
            this.options = options;
            this.confidentialClientApplication = confidentialClientApplication;

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

        public async Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            throw new NotImplementedException();
        }
    }
    public static class Office365EmailProviderExtensions
    {
        public static void RegisterOffice365Provider(this IServiceCollection services)
        {
            services.AddOptions<AuthenticationConfig>();
            services.ConfigureOptions<Office365EmailConfigureOptions>();
            services.AddTransient<IEmailProvider, Office365EmailProvider>();
        }
    }

    public class Office365EmailConfigureOptions : IConfigureOptions<AuthenticationConfig>
    {
        private readonly IConfiguration configuration;
        public Office365EmailConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

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
