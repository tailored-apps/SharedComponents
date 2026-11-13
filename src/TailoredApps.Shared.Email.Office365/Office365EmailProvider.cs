using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
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
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Shared.Email.Office365;

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
    // The two things GetMail needs from outside: an OAuth2 access token and an IMAP connection. Production
    // code gets them from MSAL and MailKit (public constructor); tests supply their own (internal constructor).
    private readonly Func<CancellationToken, Task<string>> accessTokenProvider;
    private readonly Func<IImapClient> imapClientFactory;
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
            var certificate = options.Value.Certificate
                ?? throw new InvalidOperationException($"'{AuthenticationConfig.ConfigurationKey}' needs either ClientSecret or Certificate.");
            certificateLoader.LoadIfNeeded(certificate);

            app = ConfidentialClientApplicationBuilder.Create(options.Value.ClientId)
                .WithCertificate(options.Value.Certificate.Certificate)
                .WithAuthority(new Uri(options.Value.Authority))
                .Build();
        }

        app.AddInMemoryTokenCache();

        accessTokenProvider = async cancellationToken
            => (await app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken)).AccessToken;
        imapClientFactory = static () => new ImapClient();
    }

    /// <summary>
    /// Creates a provider that takes its access token and its IMAP connection from the caller, so that
    /// <see cref="GetMail(string, string, string, TimeSpan?, CancellationToken)"/> can be exercised without
    /// Azure AD and without a mailbox.
    /// </summary>
    internal Office365EmailProvider(
        IOptions<AuthenticationConfig> options,
        Func<CancellationToken, Task<string>> accessTokenProvider,
        Func<IImapClient> imapClientFactory)
    {
        this.options = options;
        this.accessTokenProvider = accessTokenProvider;
        this.imapClientFactory = imapClientFactory;
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
    public Task<ICollection<Models.MailMessage>> GetMail(string folderName = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        => GetMail(folderName, sender, recipent, fromLast, CancellationToken.None);

    /// <summary>
    /// Retrieves e-mail messages from the configured Office 365 mailbox using IMAP with OAuth2, and stops
    /// when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// <see cref="IEmailProvider"/> declares <c>GetMail</c> without a cancellation token; this overload is on the provider itself.
    /// Reading a mailbox downloads every matching message, so a caller that can give up - a request that was
    /// aborted, a host that is shutting down - should pass its token.
    /// </remarks>
    /// <param name="folderName">The name of the IMAP sub-folder to search in. The inbox when empty.</param>
    /// <param name="sender">Optional filter: only messages whose From address contains this value are returned.</param>
    /// <param name="recipent">Optional filter: only messages whose To address contains this value are returned.</param>
    /// <param name="fromLast">Optional filter: only messages delivered within this time span are returned.</param>
    /// <param name="cancellationToken">Cancels acquiring the token and every IMAP round trip.</param>
    /// <returns>A collection of <see cref="Models.MailMessage"/> objects matching the specified criteria.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ICollection<Models.MailMessage>> GetMail(string folderName, string sender, string recipent, TimeSpan? fromLast, CancellationToken cancellationToken)
    {
        var response = new List<Models.MailMessage>();
        var accessToken = await accessTokenProvider(cancellationToken);
        var mailBox = options.Value.MailBox
            ?? throw new InvalidOperationException($"'{AuthenticationConfig.ConfigurationKey}:MailBox' is not configured.");
        var oauth2 = new SaslMechanismOAuth2(mailBox, accessToken);

        using (var client = imapClientFactory())
        {
            await client.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(oauth2, cancellationToken);
            // Every IMAP round trip is awaited: opening the folder, searching it and downloading each message
            // talk to the server, so none of them may block the calling thread.
            var folder = client.Inbox;
            if (!string.IsNullOrEmpty(folderName))
            {
                folder = await folder.GetSubfolderAsync(folderName, cancellationToken);
            }
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            var query = BuildSearchQuery(sender, recipent, fromLast, DateTime.Now);
            var uids = await folder.SearchAsync(query, cancellationToken);

            foreach (var uid in uids)
            {
                var message = await folder.GetMessageAsync(uid, cancellationToken);
                response.Add(ToMailMessage(message));
            }
            await client.DisconnectAsync(true, cancellationToken);
        }
        return response;
    }

    /// <summary>
    /// Builds the IMAP search for <see cref="GetMail(string, string, string, TimeSpan?, CancellationToken)"/>: every message, narrowed by each filter that was given.
    /// </summary>
    /// <param name="sender">Text the sender must contain; empty for any sender.</param>
    /// <param name="recipent">Text the recipient must contain; empty for any recipient.</param>
    /// <param name="fromLast">How far back to look; <c>null</c> for no limit.</param>
    /// <param name="now">The moment <paramref name="fromLast"/> is counted back from.</param>
    internal static SearchQuery BuildSearchQuery(string sender, string recipent, TimeSpan? fromLast, DateTime now)
    {
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
            query = query.And(SearchQuery.DeliveredAfter(now - fromLast.Value));
        }

        return query;
    }

    /// <summary>Maps a message read from the mailbox to the library model.</summary>
    internal static Models.MailMessage ToMailMessage(MimeMessage message)
    {
        return new Models.MailMessage
        {
            HtmlBody = message.HtmlBody,
            Topic = message.Subject,
            Copy = string.Join(",", message.Cc?.Select(z => z.ToString()) ?? []),
            Sender = string.Join(",", message.From?.Select(z => z.ToString()) ?? []),
            Recipent = string.Join(",", message.To?.Select(z => z.ToString()) ?? []),
            Body = message.GetTextBody(MimeKit.Text.TextFormat.Plain),
            Date = message.Date,
            Attachements = GetAttachements(message.Attachments)
        };
    }

    private static Dictionary<string, string> GetAttachements(IEnumerable<MimeEntity> attachments)
    {
        var result = new Dictionary<string, string>();
        foreach (var attachement in attachments)
        {
            if (attachement is MimePart { Content: not null } res)
            {
                byte[] bytes;
                using (var memoryStream = new MemoryStream())
                {
                    // DecodeTo removes the MIME transfer encoding (usually base64); copying the raw
                    // stream would hand the caller base64-of-base64.
                    res.Content.DecodeTo(memoryStream);
                    bytes = memoryStream.ToArray();
                }

                // Key by file name (as MailMessage.Attachements documents); content type is not unique.
                var name = string.IsNullOrEmpty(res.FileName) ? $"attachment-{result.Count + 1}" : res.FileName;
                var key = name;
                var suffix = 1;
                while (result.ContainsKey(key))
                {
                    key = $"{name} ({++suffix})";
                }
                result.Add(key, Convert.ToBase64String(bytes));
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
    public Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]>? attachments)
        => Task.FromException<string>(new NotImplementedException());
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
        if (section == null)
        {
            throw new System.InvalidOperationException($"Configuration section '{AuthenticationConfig.ConfigurationKey}' is missing.");
        }

        options.Instance = section.Instance;
        options.ApiUrl = section.ApiUrl;
        options.Tenant = section.Tenant;
        options.ClientId = section.ClientId;
        options.MailBox = section.MailBox;
        options.ClientSecret = section.ClientSecret;
        options.Certificate = section.Certificate;
    }
}
