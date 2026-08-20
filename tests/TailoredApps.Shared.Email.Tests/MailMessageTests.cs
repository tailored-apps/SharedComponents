using System;
using System.Collections.Generic;
using TailoredApps.Shared.Email.Models;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class MailMessageTests
    {
        [Fact]
        public void When_Constructed_Should_Have_Default_Property_Values()
        {
            // arrange
            // act
            var message = new MailMessage();

            // assert
            Assert.Null(message.Topic);
            Assert.Null(message.Sender);
            Assert.Null(message.Recipent);
            Assert.Null(message.Copy);
            Assert.Null(message.Body);
            Assert.Null(message.HtmlBody);
            Assert.Null(message.Attachements);
            Assert.Equal(default, message.Date);
        }

        [Fact]
        public void When_Properties_Are_Set_Should_Round_Trip_Values()
        {
            // arrange
            var date = new DateTimeOffset(2024, 5, 17, 12, 30, 45, TimeSpan.FromHours(2));
            var attachments = new Dictionary<string, string>
            {
                { "invoice.pdf", Convert.ToBase64String(new byte[] { 1, 2, 3 }) },
                { "logo.png", Convert.ToBase64String(new byte[] { 4, 5, 6 }) }
            };
            var message = new MailMessage();

            // act
            message.Topic = "Subject";
            message.Sender = "sender@example.com";
            message.Recipent = "recipient@example.com";
            message.Copy = "cc@example.com";
            message.Body = "Plain body";
            message.HtmlBody = "<p>Html body</p>";
            message.Attachements = attachments;
            message.Date = date;

            // assert
            Assert.Equal("Subject", message.Topic);
            Assert.Equal("sender@example.com", message.Sender);
            Assert.Equal("recipient@example.com", message.Recipent);
            Assert.Equal("cc@example.com", message.Copy);
            Assert.Equal("Plain body", message.Body);
            Assert.Equal("<p>Html body</p>", message.HtmlBody);
            Assert.Same(attachments, message.Attachements);
            Assert.Equal(2, message.Attachements.Count);
            Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), message.Attachements["invoice.pdf"]);
            Assert.Equal(date, message.Date);
            Assert.Equal(TimeSpan.FromHours(2), message.Date.Offset);
        }
    }
}
