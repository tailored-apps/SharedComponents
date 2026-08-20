using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class SmtpEmailProviderTests
    {
        private static IOptions<SmtpEmailServiceOptions> CreateOptions(SmtpEmailServiceOptions value)
        {
            // arrange helper: Moq-based IOptions accessor
            var optionsMock = new Mock<IOptions<SmtpEmailServiceOptions>>();
            optionsMock.Setup(x => x.Value).Returns(value);
            return optionsMock.Object;
        }

        [Fact]
        public void When_Constructed_With_Options_Should_Not_Throw()
        {
            // arrange
            var options = CreateOptions(new SmtpEmailServiceOptions());

            // act
            var provider = new SmtpEmailProvider(options);

            // assert
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IEmailProvider>(provider);
        }

        [Fact]
        public void When_Constructed_With_Null_Options_Should_Not_Throw()
        {
            // arrange
            // Pins current behavior: the constructor performs no argument validation,
            // a null accessor only fails later when SendMail dereferences it.
            IOptions<SmtpEmailServiceOptions> options = null;

            // act
            var provider = new SmtpEmailProvider(options);

            // assert
            Assert.NotNull(provider);
        }

        [Fact]
        public async Task When_GetMail_Is_Called_Should_Throw_NotImplementedException()
        {
            // arrange
            var provider = new SmtpEmailProvider(CreateOptions(new SmtpEmailServiceOptions()));

            // act
            var exception = await Record.ExceptionAsync(() => provider.GetMail("Inbox", "s@example.com", "r@example.com", TimeSpan.FromDays(1)));

            // assert
            Assert.IsType<NotImplementedException>(exception);
        }

        [Fact]
        public async Task When_SendMail_Is_Called_With_Null_From_Address_Should_Throw_ArgumentNullException_Before_Any_Connection()
        {
            // arrange
            // Fully offline: SendMail fails while building the MailMessage
            // (new MailAddress(null) throws) long before SmtpClient.SendMailAsync is reached,
            // so no SMTP connection is ever attempted.
            var provider = new SmtpEmailProvider(CreateOptions(new SmtpEmailServiceOptions
            {
                Host = "smtp.example.invalid",
                Port = 587,
                UserName = "user",
                Password = "password",
                From = null
            }));

            // act
            var exception = await Record.ExceptionAsync(() => provider.SendMail("to@example.com", "topic", "body", null));

            // assert
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public async Task When_SendMail_Is_Called_With_Default_Port_Should_Throw_ArgumentOutOfRangeException_Before_Any_Connection()
        {
            // arrange
            // Fully offline: with the default Port of 0 the SmtpClient.Port property setter
            // rejects the value before any connection is attempted.
            var provider = new SmtpEmailProvider(CreateOptions(new SmtpEmailServiceOptions
            {
                Host = "smtp.example.invalid",
                UserName = "user",
                Password = "password",
                From = "from@example.com"
            }));

            // act
            var exception = await Record.ExceptionAsync(() => provider.SendMail("to@example.com", "topic", "body", null));

            // assert
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }
    }
}
