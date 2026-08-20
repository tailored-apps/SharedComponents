using System;
using System.IO;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Validation;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ImageValidationExtensionTests
    {
        [Fact]
        public void When_Image_Has_Png_Header_Should_Be_Valid()
        {
            // arrange
            var image = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

            // act
            var result = image.IsValidImage();

            // assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(new byte[] { 255, 216, 255, 224, 0, 16 })] // JPEG / JFIF
        [InlineData(new byte[] { 255, 216, 255, 225, 0, 16 })] // JPEG canon (EXIF)
        public void When_Image_Has_Jpeg_Header_Should_Be_Valid(byte[] image)
        {
            // arrange - image supplied by theory data

            // act
            var result = image.IsValidImage();

            // assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(new byte[] { 66, 77, 1, 2, 3 })]      // BMP ("BM")
        [InlineData(new byte[] { 71, 73, 70, 56, 57 })]   // GIF ("GIF")
        [InlineData(new byte[] { 73, 73, 42, 0 })]        // TIFF little endian ("II*")
        [InlineData(new byte[] { 77, 77, 42, 0 })]        // TIFF big endian ("MM*")
        public void When_Image_Has_Recognised_But_Unsupported_Header_Should_Be_Invalid(byte[] image)
        {
            // arrange - image supplied by theory data

            // act
            var result = image.IsValidImage();

            // assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(new byte[] { 0, 1, 2, 3, 4 })] // no known signature
        [InlineData(new byte[] { 137 })]           // too short to be a full PNG signature
        [InlineData(new byte[] { 255, 216 })]      // truncated JPEG signature
        [InlineData(new byte[0])]                  // empty
        public void When_Image_Has_Unknown_Or_Truncated_Header_Should_Be_Invalid(byte[] image)
        {
            // arrange - image supplied by theory data

            // act
            var result = image.IsValidImage();

            // assert
            Assert.False(result);
        }

        [Fact]
        public void When_Image_Is_Null_Should_Throw_ArgumentNullException()
        {
            // arrange
            // Pins current behavior: a null array is not handled explicitly, the LINQ
            // Take() call inside the format detection throws ArgumentNullException.
            byte[] image = null;

            // act & assert
            Assert.Throws<ArgumentNullException>(() => image.IsValidImage());
        }

        [Fact]
        public void When_Real_Png_Test_Image_Is_Loaded_Should_Be_Valid()
        {
            // arrange
            var imagePath = Path.Combine(
                AppContext.BaseDirectory,
                "TestData", "ImageClassification", "TestImages", "testred.png");
            var image = File.ReadAllBytes(imagePath);

            // act
            var result = image.IsValidImage();

            // assert
            Assert.True(result);
        }
    }
}
