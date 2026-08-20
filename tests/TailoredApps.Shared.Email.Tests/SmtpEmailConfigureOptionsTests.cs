using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class SmtpEmailConfigureOptionsTests
    {
        [Fact]
        public void When_Section_Is_Present_Should_Copy_All_Values_Into_Options()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Mail:Providers:Smtp:Host", "smtp.example.com" },
                    { "Mail:Providers:Smtp:Port", "587" },
                    { "Mail:Providers:Smtp:Password", "secret" },
                    { "Mail:Providers:Smtp:EnableSsl", "true" },
                    { "Mail:Providers:Smtp:UserName", "user" },
                    { "Mail:Providers:Smtp:From", "from@example.com" },
                    { "Mail:Providers:Smtp:IsProd", "true" },
                    { "Mail:Providers:Smtp:CatchAll", "catchall@example.com" }
                })
                .Build();
            var sut = new SmtpEmailConfigureOptions(configuration);
            var options = new SmtpEmailServiceOptions();

            // act
            sut.Configure(options);

            // assert
            Assert.Equal("smtp.example.com", options.Host);
            Assert.Equal(587, options.Port);
            Assert.Equal("secret", options.Password);
            Assert.True(options.EnableSsl);
            Assert.Equal("user", options.UserName);
            Assert.Equal("from@example.com", options.From);
            Assert.True(options.IsProd);
            Assert.Equal("catchall@example.com", options.CatchAll);
        }

        [Fact]
        public void When_Section_Is_Missing_Should_Throw_Descriptive_InvalidOperationException()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>())
                .Build();
            var sut = new SmtpEmailConfigureOptions(configuration);
            var options = new SmtpEmailServiceOptions();

            // act
            var exception = Record.Exception(() => sut.Configure(options));

            // assert
            var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains(SmtpEmailServiceOptions.ConfigurationKey, invalidOperation.Message);
        }
    }
}
