namespace TailoredApps.Shared.Email
{

    /// <summary>Opcje konfiguracji dostawcy SMTP.</summary>
    public class SmtpEmailServiceOptions
    {
        /// <summary>Klucz sekcji konfiguracji.</summary>
        public static string ConfigurationKey => "Mail:Providers:Smtp";
        /// <summary>Host.</summary>
        public string Host { get; set; }
        /// <summary>Port.</summary>
        public int Port { get; set; }
        /// <summary>Password.</summary>
        public string Password { get; set; }
        /// <summary>EnableSsl.</summary>
        public bool EnableSsl { get; set; }
        /// <summary>UserName.</summary>
        public string UserName { get; set; }
        /// <summary>From.</summary>
        public string From { get; set; }
        /// <summary>IsProd.</summary>
        public bool IsProd { get; set; }
        /// <summary>CatchAll.</summary>
        public string CatchAll { get; set; }
    }
}
