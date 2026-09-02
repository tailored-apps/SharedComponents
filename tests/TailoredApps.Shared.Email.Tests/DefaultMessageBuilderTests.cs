using System.Collections.Generic;
using TailoredApps.Shared.Email.MailMessageBuilder;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class DefaultMessageBuilderTests
    {
        private readonly DefaultMessageBuilder sut = new DefaultMessageBuilder();

        [Fact]
        public void When_Template_Exists_And_Variables_Provided_Should_Return_Body_With_Tokens_Replaced()
        {
            // arrange
            var templates = new Dictionary<string, string>
            {
                { "welcome", "Hello {Name}, welcome to {Product}!" }
            };
            var variables = new Dictionary<string, string>
            {
                { "{Name}", "John" },
                { "{Product}", "TailoredApps" }
            };

            // act
            var result = sut.Build("welcome", variables, templates);

            // assert
            Assert.Equal("Hello John, welcome to TailoredApps!", result);
        }

        [Fact]
        public void When_Variable_Occurs_Multiple_Times_Should_Replace_All_Occurrences()
        {
            // arrange
            var templates = new Dictionary<string, string>
            {
                { "tpl", "{Name} and {Name} again" }
            };
            var variables = new Dictionary<string, string>
            {
                { "{Name}", "Anna" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Anna and Anna again", result);
        }

        [Fact]
        public void When_Variables_Are_Empty_Should_Return_Template_Unchanged()
        {
            // arrange
            var templates = new Dictionary<string, string>
            {
                { "tpl", "Hello {Name}!" }
            };
            var variables = new Dictionary<string, string>();

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Hello {Name}!", result);
        }

        [Fact]
        public void When_Variable_Token_Not_Present_In_Template_Should_Return_Template_Unchanged()
        {
            // arrange
            var templates = new Dictionary<string, string>
            {
                { "tpl", "Static content" }
            };
            var variables = new Dictionary<string, string>
            {
                { "{Missing}", "value" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Static content", result);
        }

        [Fact]
        public void When_Variable_Keys_Are_Raw_Strings_Should_Replace_Raw_Substrings()
        {
            // arrange
            // Pins current behavior: DefaultMessageBuilder replaces the raw variable key,
            // it does NOT wrap the key in any delimiter such as {{ }}. A bare key like "Name"
            // therefore also matches inside the word "Names".
            var templates = new Dictionary<string, string>
            {
                { "tpl", "Names: Name" }
            };
            var variables = new Dictionary<string, string>
            {
                { "Name", "X" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Xs: X", result);
        }

        [Fact]
        public void When_TemplateKey_Not_Found_Should_Throw_KeyNotFoundException()
        {
            // arrange
            var templates = new Dictionary<string, string>
            {
                { "known", "content" }
            };
            var variables = new Dictionary<string, string>();

            // act
            var exception = Assert.Throws<KeyNotFoundException>(() => sut.Build("unknown", variables, templates));

            // assert
            Assert.Contains("was not found", exception.Message);
        }

        [Fact]
        public void When_Templates_Are_Empty_Should_Throw_KeyNotFoundException()
        {
            // arrange
            var templates = new Dictionary<string, string>();
            var variables = new Dictionary<string, string> { { "{Name}", "John" } };

            // act
            var exception = Assert.Throws<KeyNotFoundException>(() => sut.Build("any", variables, templates));

            // assert
            Assert.Contains("was not found", exception.Message);
        }
    }
}
