using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ModelInfoServiceTests
    {
        private const string ModelFilePath = @"c:\models\image.zip";

        private readonly Mock<IModelHelper> modelHelperMock;
        private readonly ModelInfoService sut;

        public ModelInfoServiceTests()
        {
            modelHelperMock = new Mock<IModelHelper>(MockBehavior.Strict);
            var options = Options.Create(new ImageClassificationOptions { ModelFilePath = ModelFilePath });
            sut = new ModelInfoService(options, modelHelperMock.Object);
        }

        [Fact]
        public void When_ModelFilePath_Is_Read_Should_Return_Configured_Path()
        {
            // arrange - done in constructor

            // act
            var result = sut.ModelFilePath;

            // assert
            Assert.Equal(ModelFilePath, result);
        }

        [Fact]
        public void When_ModelFileName_Is_Read_Should_Return_File_Name_Without_Directory()
        {
            // arrange - done in constructor

            // act
            var result = sut.ModelFileName;

            // assert
            Assert.Equal("image.zip", result);
        }

        [Fact]
        public void When_ModelChecksum_Is_Read_Should_Delegate_To_Model_Helper_With_Configured_Path()
        {
            // arrange
            modelHelperMock.Setup(m => m.GetChecksum(ModelFilePath)).Returns("abc123");

            // act
            var result = sut.ModelChecksum;

            // assert
            Assert.Equal("abc123", result);
            modelHelperMock.Verify(m => m.GetChecksum(ModelFilePath), Times.Once);
        }

        [Fact]
        public void When_ModelVersion_Is_Read_Should_Delegate_To_Model_Helper_With_Configured_Path()
        {
            // arrange
            modelHelperMock.Setup(m => m.GetVersion(ModelFilePath)).Returns("20260820.101010");

            // act
            var result = sut.ModelVersion;

            // assert
            Assert.Equal("20260820.101010", result);
            modelHelperMock.Verify(m => m.GetVersion(ModelFilePath), Times.Once);
        }

        [Fact]
        public void When_Labels_Is_Read_Should_Delegate_To_Model_Helper_With_Configured_Path()
        {
            // arrange
            var labels = new[] { "red", "green", "blue" };
            modelHelperMock.Setup(m => m.GetLabels(ModelFilePath)).Returns(labels);

            // act
            var result = sut.Labels;

            // assert
            Assert.Same(labels, result);
            modelHelperMock.Verify(m => m.GetLabels(ModelFilePath), Times.Once);
        }
    }
}
