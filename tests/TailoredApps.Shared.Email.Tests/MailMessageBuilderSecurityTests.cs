using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.Email.MailMessageBuilder;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class TokenReplacingMailMessageBuilderSecurityTests
    {
        private static TokenReplacingMailMessageBuilder Builder(bool encode = true) =>
            new(Options.Create(new TokenReplacingMailMessageBuilderOptions { HtmlEncodeVariables = encode }));

        [Fact]
        public void When_Variable_Contains_Html_Should_Encode_It_By_Default()
        {
            // arrange
            var templates = new Dictionary<string, string> { { "tpl", "Hello {{Name}}!" } };
            var variables = new Dictionary<string, string> { { "Name", "<a href=\"https://evil.example\">Verify</a>" } };

            // act
            var result = Builder().Build("tpl", variables, templates);

            // assert
            Assert.Equal("Hello &lt;a href=&quot;https://evil.example&quot;&gt;Verify&lt;/a&gt;!", result);
        }

        [Fact]
        public void When_Triple_Braces_Are_Used_Should_Insert_Raw_Html()
        {
            // arrange
            var templates = new Dictionary<string, string> { { "tpl", "<div>{{{Body}}}</div>" } };
            var variables = new Dictionary<string, string> { { "Body", "<b>bold</b>" } };

            // act
            var result = Builder().Build("tpl", variables, templates);

            // assert
            Assert.Equal("<div><b>bold</b></div>", result);
        }

        [Fact]
        public void When_Encoding_Disabled_In_Options_Should_Insert_Raw_Value()
        {
            // arrange
            var templates = new Dictionary<string, string> { { "tpl", "{{Name}}" } };
            var variables = new Dictionary<string, string> { { "Name", "<i>x</i>" } };

            // act
            var result = Builder(encode: false).Build("tpl", variables, templates);

            // assert
            Assert.Equal("<i>x</i>", result);
        }

        [Fact]
        public void When_Value_Contains_Another_Token_Should_Not_Expand_It()
        {
            // arrange
            var templates = new Dictionary<string, string> { { "tpl", "{{A}} / {{B}}" } };
            var variables = new Dictionary<string, string> { { "A", "{{B}}" }, { "B", "secret" } };

            // act
            var result = Builder(encode: false).Build("tpl", variables, templates);

            // assert - single pass: the injected token text stays literal
            Assert.Equal("{{B}} / secret", result);
        }

        [Fact]
        public void When_Variables_Are_Null_Should_Leave_Tokens_Untouched()
        {
            var templates = new Dictionary<string, string> { { "tpl", "Hi {{Name}}" } };
            var result = Builder().Build("tpl", null, templates);
            Assert.Equal("Hi {{Name}}", result);
        }
    }

    public class DefaultMessageBuilderSecurityTests
    {
        [Fact]
        public void When_Variable_Contains_Html_Should_Encode_It_By_Default()
        {
            var templates = new Dictionary<string, string> { { "tpl", "Hello Name!" } };
            var variables = new Dictionary<string, string> { { "Name", "<script>alert(1)</script>" } };

            var result = new DefaultMessageBuilder().Build("tpl", variables, templates);

            Assert.Equal("Hello &lt;script&gt;alert(1)&lt;/script&gt;!", result);
        }

        [Fact]
        public void When_Encoding_Disabled_Should_Insert_Raw_Value()
        {
            var templates = new Dictionary<string, string> { { "tpl", "Hello Name!" } };
            var variables = new Dictionary<string, string> { { "Name", "<b>x</b>" } };

            var result = new DefaultMessageBuilder(htmlEncodeVariables: false).Build("tpl", variables, templates);

            Assert.Equal("Hello <b>x</b>!", result);
        }

        [Fact]
        public void When_Value_Contains_Another_Key_Should_Not_Expand_It()
        {
            var templates = new Dictionary<string, string> { { "tpl", "A and B" } };
            var variables = new Dictionary<string, string> { { "A", "B" }, { "B", "C" } };

            var result = new DefaultMessageBuilder(htmlEncodeVariables: false).Build("tpl", variables, templates);

            Assert.Equal("B and C", result);
        }

        [Fact]
        public void When_Keys_Overlap_Should_Prefer_Longest_Key()
        {
            var templates = new Dictionary<string, string> { { "tpl", "UserName User" } };
            var variables = new Dictionary<string, string> { { "User", "u" }, { "UserName", "un" } };

            var result = new DefaultMessageBuilder(htmlEncodeVariables: false).Build("tpl", variables, templates);

            Assert.Equal("un u", result);
        }
    }

    public class SmtpEmailProviderRecipientTests
    {
        private static SmtpEmailProvider Provider(bool isProd, string catchAll = null) =>
            new(Options.Create(new SmtpEmailServiceOptions
            {
                Host = "smtp.invalid",
                Port = 25,
                From = "noreply@example.com",
                IsProd = isProd,
                CatchAll = catchAll,
            }));

        [Fact]
        public async Task When_Recipient_Is_A_List_Should_Reject_Before_Sending()
        {
            // MailAddress accepts exactly one address; a comma-separated list is a FormatException.
            await Assert.ThrowsAsync<FormatException>(() =>
                Provider(isProd: true).SendMail("victim@example.com, attacker@evil.example", "s", "b", null));
        }

        [Fact]
        public async Task When_Not_Production_And_CatchAll_Missing_Should_Throw_Instead_Of_Sending_Nowhere()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Provider(isProd: false).SendMail("victim@example.com", "s", "b", null));
        }

        [Fact]
        public void When_Options_Are_Default_Should_Enable_Tls()
        {
            Assert.True(new SmtpEmailServiceOptions().EnableSsl);
        }
    }
}
