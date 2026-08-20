using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class Office365EmailProviderTests
    {
        private const string ClientSecretPlaceholder = "[Enter here a client secret for your application]";

        private static IOptions<AuthenticationConfig> CreateOptions(AuthenticationConfig config)
        {
            // arrange helper: Moq-based IOptions accessor
            var optionsMock = new Mock<IOptions<AuthenticationConfig>>();
            optionsMock.Setup(x => x.Value).Returns(config);
            return optionsMock.Object;
        }

        private static AuthenticationConfig CreateValidClientSecretConfig()
        {
            return new AuthenticationConfig
            {
                Tenant = "11111111-2222-3333-4444-555555555555",
                ClientId = "99999999-8888-7777-6666-555555555555",
                ClientSecret = "real-looking-client-secret",
                MailBox = "mailbox@contoso.com"
            };
        }

        [Fact]
        public void When_Neither_ClientSecret_Nor_Certificate_Is_Configured_Should_Throw_Exception()
        {
            // arrange
            var config = new AuthenticationConfig
            {
                Tenant = "11111111-2222-3333-4444-555555555555",
                ClientId = "99999999-8888-7777-6666-555555555555"
            };
            var options = CreateOptions(config);

            // act
            var exception = Assert.Throws<Exception>(() => new Office365EmailProvider(options));

            // assert
            Assert.Equal("You must choose between using client secret or certificate. Please update appsettings.json file.", exception.Message);
        }

        [Fact]
        public void When_ClientSecret_Is_Placeholder_And_No_Certificate_Should_Throw_Exception()
        {
            // arrange
            var config = new AuthenticationConfig
            {
                Tenant = "11111111-2222-3333-4444-555555555555",
                ClientId = "99999999-8888-7777-6666-555555555555",
                ClientSecret = ClientSecretPlaceholder
            };
            var options = CreateOptions(config);

            // act
            var exception = Assert.Throws<Exception>(() => new Office365EmailProvider(options));

            // assert
            Assert.Equal("You must choose between using client secret or certificate. Please update appsettings.json file.", exception.Message);
        }

        [Fact]
        public void When_ClientSecret_Is_Whitespace_And_No_Certificate_Should_Throw_Exception()
        {
            // arrange
            var config = new AuthenticationConfig
            {
                Tenant = "11111111-2222-3333-4444-555555555555",
                ClientId = "99999999-8888-7777-6666-555555555555",
                ClientSecret = "   "
            };
            var options = CreateOptions(config);

            // act
            var exception = Assert.Throws<Exception>(() => new Office365EmailProvider(options));

            // assert
            Assert.Equal("You must choose between using client secret or certificate. Please update appsettings.json file.", exception.Message);
        }

        [Fact]
        public void When_Real_Looking_ClientSecret_Is_Configured_Should_Construct_Successfully()
        {
            // arrange
            // ConfidentialClientApplicationBuilder.Build() performs no network I/O,
            // so constructing the provider with a valid-looking secret is fully offline.
            var options = CreateOptions(CreateValidClientSecretConfig());

            // act
            var provider = new Office365EmailProvider(options);

            // assert
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IEmailProvider>(provider);
        }

        [Fact]
        public async Task When_SendMail_Is_Called_Should_Throw_NotImplementedException()
        {
            // arrange
            var provider = new Office365EmailProvider(CreateOptions(CreateValidClientSecretConfig()));

            // act
            var exception = await Record.ExceptionAsync(() => provider.SendMail("to@example.com", "topic", "body", null));

            // assert
            Assert.IsType<NotImplementedException>(exception);
        }
    }
}
