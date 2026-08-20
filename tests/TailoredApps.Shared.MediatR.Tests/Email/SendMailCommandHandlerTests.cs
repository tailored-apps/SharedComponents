using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.MediatR.Email.Handlers;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class SendMailCommandHandlerTests
    {
        private static SendMail CreateCommand() => new SendMail
        {
            Recipent = "recipient@example.com",
            Subject = "Monthly report",
            Template = "report-template",
            TemplateVariables = new Dictionary<string, string> { ["userName"] = "Jan" },
            Templates = new Dictionary<string, string> { ["report-template"] = "Hello {{userName}}" },
            Attachments = new Dictionary<string, byte[]> { ["report.pdf"] = new byte[] { 1, 2, 3 } },
        };

        [Fact]
        public async Task When_Handling_SendMail_Should_Build_Body_From_Template_And_Return_Provider_MessageId()
        {
            // arrange
            var command = CreateCommand();
            var builderMock = new Mock<IMailMessageBuilder>(MockBehavior.Strict);
            builderMock
                .Setup(b => b.Build(command.Template, command.TemplateVariables, command.Templates))
                .Returns("built-body");
            var providerMock = new Mock<IEmailProvider>(MockBehavior.Strict);
            providerMock
                .Setup(p => p.SendMail(command.Recipent, command.Subject, "built-body", command.Attachments))
                .ReturnsAsync("message-id-123");
            var sut = new SendMailCommandHandler(providerMock.Object, builderMock.Object);

            // act
            var response = await sut.Handle(command, CancellationToken.None);

            // assert - the builder receives Template/TemplateVariables/Templates verbatim (same instances),
            // its output becomes the message body, and the provider's message id is returned
            Assert.Equal("message-id-123", response.MessageId);
            builderMock.Verify(b => b.Build(command.Template, command.TemplateVariables, command.Templates), Times.Once);
            providerMock.Verify(p => p.SendMail(command.Recipent, command.Subject, "built-body", command.Attachments), Times.Once);
        }

        [Fact]
        public async Task When_Handling_SendMail_Should_Forward_Arguments_To_Provider_In_Correct_Parameter_Order()
        {
            // arrange
            var command = CreateCommand();
            var builderMock = new Mock<IMailMessageBuilder>();
            builderMock
                .Setup(b => b.Build(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<IDictionary<string, string>>()))
                .Returns("built-body");
            (string Recipent, string Topic, string Body, Dictionary<string, byte[]> Attachments) captured = default;
            var providerMock = new Mock<IEmailProvider>();
            providerMock
                .Setup(p => p.SendMail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, byte[]>>()))
                .Callback<string, string, string, Dictionary<string, byte[]>>(
                    (recipnet, topic, messageBody, attachments) => captured = (recipnet, topic, messageBody, attachments))
                .ReturnsAsync("message-id");
            var sut = new SendMailCommandHandler(providerMock.Object, builderMock.Object);

            // act
            await sut.Handle(command, CancellationToken.None);

            // assert - guards against parameter-order regressions:
            // IEmailProvider.SendMail(recipnet, topic, messageBody, attachments)
            Assert.Equal("recipient@example.com", captured.Recipent);
            Assert.Equal("Monthly report", captured.Topic);
            Assert.Equal("built-body", captured.Body);
            Assert.Same(command.Attachments, captured.Attachments);
        }

        [Fact]
        public async Task When_Provider_Throws_Should_Propagate_Exception()
        {
            // arrange
            var command = CreateCommand();
            var builderMock = new Mock<IMailMessageBuilder>();
            builderMock
                .Setup(b => b.Build(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<IDictionary<string, string>>()))
                .Returns("built-body");
            var providerMock = new Mock<IEmailProvider>();
            providerMock
                .Setup(p => p.SendMail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, byte[]>>()))
                .ThrowsAsync(new InvalidOperationException("smtp unavailable"));
            var sut = new SendMailCommandHandler(providerMock.Object, builderMock.Object);

            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.Handle(command, CancellationToken.None));

            // assert
            Assert.Equal("smtp unavailable", exception.Message);
        }
    }
}
