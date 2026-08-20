using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class AuthenticationConfigTests
    {
        [Fact]
        public void Should_Expose_Expected_ConfigurationKey()
        {
            // arrange
            // act
            var key = AuthenticationConfig.ConfigurationKey;

            // assert
            Assert.Equal("Mail:Providers:Office365", key);
        }

        [Fact]
        public void When_Constructed_Should_Have_Default_Instance_And_ApiUrl()
        {
            // arrange
            // act
            var config = new AuthenticationConfig();

            // assert
            Assert.Equal("https://login.microsoftonline.com/{0}", config.Instance);
            Assert.Equal("https://graph.microsoft.com/", config.ApiUrl);
            Assert.Null(config.Tenant);
            Assert.Null(config.ClientId);
            Assert.Null(config.MailBox);
            Assert.Null(config.ClientSecret);
            Assert.Null(config.Certificate);
        }

        [Fact]
        public void When_Tenant_Is_Set_Should_Compose_Authority_From_Instance_Template()
        {
            // arrange
            var config = new AuthenticationConfig
            {
                Tenant = "contoso.onmicrosoft.com"
            };

            // act
            var authority = config.Authority;

            // assert
            Assert.Equal("https://login.microsoftonline.com/contoso.onmicrosoft.com", authority);
        }

        [Fact]
        public void When_Custom_Instance_Is_Set_Should_Format_Tenant_Into_Custom_Template()
        {
            // arrange
            var config = new AuthenticationConfig
            {
                Instance = "https://login.microsoftonline.us/{0}",
                Tenant = "11111111-2222-3333-4444-555555555555"
            };

            // act
            var authority = config.Authority;

            // assert
            Assert.Equal("https://login.microsoftonline.us/11111111-2222-3333-4444-555555555555", authority);
        }

        [Fact]
        public void When_Tenant_Is_Null_Should_Format_Authority_With_Empty_Placeholder()
        {
            // arrange
            var config = new AuthenticationConfig();

            // act
            var authority = config.Authority;

            // assert
            Assert.Equal("https://login.microsoftonline.com/", authority);
        }

        [Fact]
        public void When_Properties_Are_Set_Should_Round_Trip_Values()
        {
            // arrange
            var config = new AuthenticationConfig();

            // act
            config.Instance = "https://example.com/{0}";
            config.ApiUrl = "https://api.example.com/";
            config.Tenant = "tenant-id";
            config.ClientId = "client-id";
            config.MailBox = "mailbox@example.com";
            config.ClientSecret = "secret";

            // assert
            Assert.Equal("https://example.com/{0}", config.Instance);
            Assert.Equal("https://api.example.com/", config.ApiUrl);
            Assert.Equal("tenant-id", config.Tenant);
            Assert.Equal("client-id", config.ClientId);
            Assert.Equal("mailbox@example.com", config.MailBox);
            Assert.Equal("secret", config.ClientSecret);
        }
    }
}
