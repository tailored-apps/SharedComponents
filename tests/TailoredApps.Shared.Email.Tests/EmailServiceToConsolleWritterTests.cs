using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class EmailServiceToConsolleWritterTests
    {
        private readonly EmailServiceToConsolleWritter sut = new EmailServiceToConsolleWritter();

        [Fact]
        public async Task When_SendMail_Is_Called_Should_Write_Formatted_Message_To_Console()
        {
            // arrange
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                // act
                await sut.SendMail("john@example.com", "Hello topic", "Body text", null);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            // assert
            Assert.Equal(
                "recipent: john@example.com; topic: Hello topic; message: Body text" + Environment.NewLine,
                writer.ToString());
        }

        [Fact]
        public async Task When_SendMail_Is_Called_Should_Return_Formatted_Message_String()
        {
            // arrange
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            string result;
            try
            {
                // act
                result = await sut.SendMail("john@example.com", "Hello topic", "Body text", new Dictionary<string, byte[]>());
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            // assert
            Assert.Equal("recipent: john@example.com; topic: Hello topic; message: Body text", result);
        }

        [Fact]
        public async Task When_SendMail_Is_Called_With_Attachments_Should_Ignore_Them()
        {
            // arrange
            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            var attachments = new Dictionary<string, byte[]>
            {
                { "file.txt", new byte[] { 1, 2, 3 } }
            };
            string result;
            try
            {
                // act
                result = await sut.SendMail("a@b.pl", "t", "m", attachments);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            // assert
            Assert.Equal("recipent: a@b.pl; topic: t; message: m", result);
            Assert.DoesNotContain("file.txt", writer.ToString());
        }

        [Fact]
        public async Task When_GetMail_Is_Called_Should_Return_Empty_Collection()
        {
            // arrange
            // act
            var result = await sut.GetMail();

            // assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task When_GetMail_Is_Called_With_Filters_Should_Still_Return_Empty_Collection()
        {
            // arrange
            // act
            var result = await sut.GetMail("Inbox", "sender@example.com", "recipient@example.com", TimeSpan.FromDays(1));

            // assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
