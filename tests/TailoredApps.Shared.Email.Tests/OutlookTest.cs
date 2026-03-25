using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class OutlookTest
    {
        [Fact(Skip = "setup config before test")]
        public async Task GetEmailTestFromOutlook()
        {
            var build = Host.CreateDefaultBuilder()
                  .ConfigureAppConfiguration(z => z.AddEnvironmentVariables())
                  .ConfigureServices((_, services) =>
                  {
                      services.RegisterOffice365Provider();
                  }).Build();
            var provider = build.Services.GetService<IEmailProvider>();
            var uids = await provider.GetMail(fromLast: TimeSpan.FromDays(4));
            foreach (var message in uids)
            {
                Assert.True(!string.IsNullOrEmpty(message.HtmlBody));
            }
        }
    }
}
