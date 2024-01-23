using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TailoredApps.Shared.Email.Office365;
using System.Threading.Tasks;
using System;

namespace TailoredApps.Shared.Email.Tests
{
    public class SmtpTest
    {
        [Fact(Skip ="please do not send dumb data from build")]
        public async Task SendEmailTestFromSmtp()
        {
            var build = Host.CreateDefaultBuilder()
                  .ConfigureAppConfiguration(z => z.AddEnvironmentVariables())
                  .ConfigureServices((_, services) =>
                  {
                      services.RegisterSmtpProvider();
                  }).Build();
            var provider = build.Services.GetService<IEmailProvider>();
            await provider.SendMail("test@adress.pl","test email","test body", null);
        }
    }
}
