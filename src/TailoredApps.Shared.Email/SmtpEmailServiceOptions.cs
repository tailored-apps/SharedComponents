namespace TailoredApps.Shared.Email
{
    /// <summary>
    /// Configuration options for the SMTP email provider.
    /// Bind this class to the configuration section identified by <see cref="ConfigurationKey"/>.
    /// </summary>
    public class SmtpEmailServiceOptions
    {
        /// <summary>
        /// Gets the configuration section key used to bind these options from the application settings.
        /// The value is <c>"Mail:Providers:Smtp"</c>.
        /// </summary>
        public static string ConfigurationKey => "Mail:Providers:Smtp";

        /// <summary>
        /// Gets or sets the hostname or IP address of the SMTP server.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the port number used to connect to the SMTP server.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the password used to authenticate with the SMTP server.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether SSL/TLS encryption is enabled for the SMTP connection.
        /// </summary>
        public bool EnableSsl { get; set; }

        /// <summary>
        /// Gets or sets the username used to authenticate with the SMTP server.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the email address used as the sender (From/Sender header) of outgoing messages.
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application is running in a production environment.
        /// When <c>true</c>, emails are sent to the actual recipient; otherwise they are redirected to <see cref="CatchAll"/>.
        /// </summary>
        public bool IsProd { get; set; }

        /// <summary>
        /// Gets or sets the catch-all email address used as the recipient in non-production environments.
        /// All outgoing messages are redirected to this address when <see cref="IsProd"/> is <c>false</c>.
        /// </summary>
        public string CatchAll { get; set; }
    }
}
