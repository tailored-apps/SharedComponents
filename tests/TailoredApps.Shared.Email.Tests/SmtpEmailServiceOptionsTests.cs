using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class SmtpEmailServiceOptionsTests
    {
        [Fact]
        public void Should_Expose_Expected_ConfigurationKey()
        {
            // arrange
            // act
            var key = SmtpEmailServiceOptions.ConfigurationKey;

            // assert
            Assert.Equal("Mail:Providers:Smtp", key);
        }

        [Fact]
        public void When_Constructed_Should_Have_Default_Property_Values()
        {
            // arrange
            // act
            var options = new SmtpEmailServiceOptions();

            // assert
            Assert.Null(options.Host);
            Assert.Equal(0, options.Port);
            Assert.Null(options.Password);
            Assert.False(options.EnableSsl);
            Assert.Null(options.UserName);
            Assert.Null(options.From);
            Assert.False(options.IsProd);
            Assert.Null(options.CatchAll);
        }

        [Fact]
        public void When_Properties_Are_Set_Should_Round_Trip_Values()
        {
            // arrange
            var options = new SmtpEmailServiceOptions();

            // act
            options.Host = "smtp.example.com";
            options.Port = 587;
            options.Password = "secret";
            options.EnableSsl = true;
            options.UserName = "user";
            options.From = "from@example.com";
            options.IsProd = true;
            options.CatchAll = "catchall@example.com";

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
    }
}
