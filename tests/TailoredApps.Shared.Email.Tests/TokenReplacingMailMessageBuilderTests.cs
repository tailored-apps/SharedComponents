using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.Email.MailMessageBuilder;
using Xunit;

namespace TailoredApps.Shared.Email.Tests
{
    public class TokenReplacingMailMessageBuilderTests : IDisposable
    {
        private readonly string tempDirectory;

        public TokenReplacingMailMessageBuilderTests()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "TokenReplacingMailMessageBuilderTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static TokenReplacingMailMessageBuilder CreateSut(TokenReplacingMailMessageBuilderOptions optionsValue)
        {
            // arrange helper: Moq-based IOptions accessor
            var optionsMock = new Mock<IOptions<TokenReplacingMailMessageBuilderOptions>>();
            optionsMock.Setup(x => x.Value).Returns(optionsValue);
            return new TokenReplacingMailMessageBuilder(optionsMock.Object);
        }

        [Fact]
        public void When_Template_Provided_In_Dictionary_Should_Replace_Curly_Brace_Tokens()
        {
            // arrange
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());
            var templates = new Dictionary<string, string>
            {
                { "welcome", "Hello {{Name}}, welcome to {{Product}}!" }
            };
            var variables = new Dictionary<string, string>
            {
                { "Name", "John" },
                { "Product", "TailoredApps" }
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
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());
            var templates = new Dictionary<string, string>
            {
                { "tpl", "{{Name}} meets {{Name}}" }
            };
            var variables = new Dictionary<string, string>
            {
                { "Name", "Anna" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Anna meets Anna", result);
        }

        [Fact]
        public void When_Template_Contains_Token_Without_Matching_Variable_Should_Leave_Token_Untouched()
        {
            // arrange
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());
            var templates = new Dictionary<string, string>
            {
                { "tpl", "Hello {{Name}}, your code is {{Code}}" }
            };
            var variables = new Dictionary<string, string>
            {
                { "Name", "John" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Hello John, your code is {{Code}}", result);
        }

        [Fact]
        public void When_Variable_Key_Appears_Without_Braces_In_Template_Should_Not_Replace_It()
        {
            // arrange
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());
            var templates = new Dictionary<string, string>
            {
                { "tpl", "Name is {{Name}}" }
            };
            var variables = new Dictionary<string, string>
            {
                { "Name", "John" }
            };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Name is John", result);
        }

        [Fact]
        public void When_TemplateKey_Not_Found_Should_Throw_KeyNotFoundException()
        {
            // arrange
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());
            var templates = new Dictionary<string, string> { { "known", "content" } };

            // act
            var exception = Assert.Throws<KeyNotFoundException>(() => sut.Build("unknown", new Dictionary<string, string>(), templates));

            // assert
            Assert.Contains("was not found", exception.Message);
        }

        [Fact]
        public void When_Templates_Dictionary_Is_Null_And_No_Location_Configured_Should_Throw_KeyNotFoundException()
        {
            // arrange
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions());

            // act
            var exception = Assert.Throws<KeyNotFoundException>(() => sut.Build("any", new Dictionary<string, string>(), null));

            // assert
            Assert.Contains("was not found", exception.Message);
        }

        [Fact]
        public void When_Constructed_With_Null_Options_Should_Still_Build_From_Provided_Templates()
        {
            // arrange
            var sut = new TokenReplacingMailMessageBuilder(null);
            var templates = new Dictionary<string, string> { { "tpl", "Hi {{Name}}" } };
            var variables = new Dictionary<string, string> { { "Name", "John" } };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Hi John", result);
        }

        [Fact]
        public void When_Options_Value_Is_Null_Should_Still_Build_From_Provided_Templates()
        {
            // arrange
            var sut = CreateSut(null);
            var templates = new Dictionary<string, string> { { "tpl", "Hi {{Name}}" } };
            var variables = new Dictionary<string, string> { { "Name", "John" } };

            // act
            var result = sut.Build("tpl", variables, templates);

            // assert
            Assert.Equal("Hi John", result);
        }

        [Fact]
        public void When_Location_Contains_Single_File_Matching_TemplateKey_Should_Load_Template_From_Disk()
        {
            // arrange
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(Path.Combine(tempDirectory, "welcome.html"), "Hello {{Name}} from disk");
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions
            {
                Location = tempDirectory,
                FileExtension = "html"
            });
            var variables = new Dictionary<string, string> { { "Name", "John" } };

            // act
            var result = sut.Build("welcome.html", variables, new Dictionary<string, string>());

            // assert
            Assert.Equal("Hello John from disk", result);
        }

        [Fact]
        public void When_Location_Contains_File_With_Different_Name_Should_Throw_KeyNotFoundException()
        {
            // arrange
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(Path.Combine(tempDirectory, "actual.html"), "content of actual.html {{Name}}");
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions
            {
                Location = tempDirectory,
                FileExtension = "html"
            });
            var variables = new Dictionary<string, string> { { "Name", "John" } };

            // act
            var exception = Record.Exception(() => sut.Build("nonexistent.html", variables, new Dictionary<string, string>()));

            // assert
            Assert.IsType<KeyNotFoundException>(exception);
        }

        [Fact]
        public void When_Location_Contains_Multiple_Files_Should_Register_Each_Under_Its_File_Name()
        {
            // arrange
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(Path.Combine(tempDirectory, "aaa.html"), "first {{Name}}");
            File.WriteAllText(Path.Combine(tempDirectory, "zzz.html"), "second {{Name}}");
            var sut = CreateSut(new TokenReplacingMailMessageBuilderOptions
            {
                Location = tempDirectory,
                FileExtension = "html"
            });
            var variables = new Dictionary<string, string> { { "Name", "John" } };

            // act
            var first = sut.Build("aaa.html", variables, new Dictionary<string, string>());
            var second = sut.Build("zzz.html", variables, new Dictionary<string, string>());

            // assert
            Assert.Equal("first John", first);
            Assert.Equal("second John", second);
        }
    }
}
