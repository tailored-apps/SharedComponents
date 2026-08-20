using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class Office365EmailConfigureOptionsTests
    {
        [Fact]
        public void When_Section_Is_Present_Should_Copy_All_Values_Into_Options()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Mail:Providers:Office365:Instance", "https://login.microsoftonline.us/{0}" },
                    { "Mail:Providers:Office365:ApiUrl", "https://graph.microsoft.us/" },
                    { "Mail:Providers:Office365:Tenant", "contoso.onmicrosoft.com" },
                    { "Mail:Providers:Office365:ClientId", "11111111-2222-3333-4444-555555555555" },
                    { "Mail:Providers:Office365:MailBox", "mailbox@contoso.com" },
                    { "Mail:Providers:Office365:ClientSecret", "super-secret-value" }
                })
                .Build();
            var sut = new Office365EmailConfigureOptions(configuration);
            var options = new AuthenticationConfig();

            // act
            sut.Configure(options);

            // assert
            Assert.Equal("https://login.microsoftonline.us/{0}", options.Instance);
            Assert.Equal("https://graph.microsoft.us/", options.ApiUrl);
            Assert.Equal("contoso.onmicrosoft.com", options.Tenant);
            Assert.Equal("11111111-2222-3333-4444-555555555555", options.ClientId);
            Assert.Equal("mailbox@contoso.com", options.MailBox);
            Assert.Equal("super-secret-value", options.ClientSecret);
            Assert.Null(options.Certificate);
        }

        [Fact]
        public void When_Section_Is_Missing_Should_Throw_Descriptive_InvalidOperationException()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>())
                .Build();
            var sut = new Office365EmailConfigureOptions(configuration);
            var options = new AuthenticationConfig();

            // act
            var exception = Record.Exception(() => sut.Configure(options));

            // assert
            var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains(AuthenticationConfig.ConfigurationKey, invalidOperation.Message);
        }
    }
}
