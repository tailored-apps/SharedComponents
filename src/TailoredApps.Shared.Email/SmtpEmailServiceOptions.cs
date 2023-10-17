namespace TailoredApps.Shared.Email
{

    public class SmtpEmailServiceOptions
    {
        public static string ConfigurationKey => "Mail:Providers:Smtp";
        public string Host { get; set; }
        public int Port { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; }
        public string UserName { get; set; }
        public string From { get; set; }
        public bool IsProd { get; set; }
        public string CatchAll { get; set; }
    }
}
