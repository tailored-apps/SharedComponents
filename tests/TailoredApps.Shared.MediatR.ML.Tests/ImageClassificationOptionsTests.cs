using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ImageClassificationOptionsTests
    {
        [Fact]
        public void Should_Expose_Expected_Configuration_Keys()
        {
            // arrange - constants are compile time values

            // act & assert
            Assert.Equal("ImageClassification", ImageClassificationOptions.ConfigurationKey);
            Assert.Equal("ImageClassification:ModelFilePath", ImageClassificationOptions.ModelFilePathConfig);
        }

        [Fact]
        public void When_ModelFilePath_Is_Set_Should_Round_Trip_The_Value()
        {
            // arrange
            var options = new ImageClassificationOptions();

            // act
            options.ModelFilePath = @"c:\models\image.zip";

            // assert
            Assert.Equal(@"c:\models\image.zip", options.ModelFilePath);
        }

        [Fact]
        public void Should_Default_ModelFilePath_To_Null()
        {
            // arrange & act
            var options = new ImageClassificationOptions();

            // assert
            Assert.Null(options.ModelFilePath);
        }

        [Fact]
        public void When_Configuration_Contains_Section_Configure_Should_Bind_ModelFilePath()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [ImageClassificationOptions.ModelFilePathConfig] = @"c:\models\image.zip"
                })
                .Build();
            var sut = new ImageClassificationOptions.ImageClassificationConfigureOptions(configuration);
            var options = new ImageClassificationOptions();

            // act
            sut.Configure(options);

            // assert
            Assert.Equal(@"c:\models\image.zip", options.ModelFilePath);
        }

        [Fact]
        public void When_Configuration_Section_Is_Missing_Configure_Should_Throw_Descriptive_InvalidOperationException()
        {
            // arrange
            var configuration = new ConfigurationBuilder().Build();
            var sut = new ImageClassificationOptions.ImageClassificationConfigureOptions(configuration);
            var options = new ImageClassificationOptions();

            // act
            var exception = Assert.Throws<InvalidOperationException>(() => sut.Configure(options));

            // assert
            Assert.Contains(ImageClassificationOptions.ConfigurationKey, exception.Message);
        }
    }
}
