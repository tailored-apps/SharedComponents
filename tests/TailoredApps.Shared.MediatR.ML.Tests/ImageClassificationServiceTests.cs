using System;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ImageClassificationServiceTests
    {
        private readonly Mock<IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore>> predictionEnginePoolMock;
        private readonly Mock<IModelInfoService> modelInfoServiceMock;
        private readonly ImageClassificationService sut;

        public ImageClassificationServiceTests()
        {
            predictionEnginePoolMock = new Mock<IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore>>();
            modelInfoServiceMock = new Mock<IModelInfoService>();
            sut = new ImageClassificationService(predictionEnginePoolMock.Object, modelInfoServiceMock.Object);
        }

        [Fact]
        public void When_Predict_Is_Called_Should_Map_Prediction_To_ImagePrediction()
        {
            // arrange
            var imageBytes = new byte[] { 1, 2, 3 };
            predictionEnginePoolMock
                .Setup(p => p.Predict(It.IsAny<InMemoryImageData>()))
                .Returns(new ImagePredictionScore
                {
                    PredictedLabel = "red",
                    Score = new[] { 0.1f, 0.7f, 0.2f }
                });

            // act
            var prediction = sut.Predict(imageBytes, "testred.png");

            // assert
            Assert.Equal("testred.png", prediction.FileName);
            Assert.Equal("red", prediction.PredictedLabel);
            Assert.Equal(0.7f, prediction.PredictedScore);
            Assert.Null(prediction.Scores);
            Assert.Null(prediction.ModelInfo);
        }

        [Fact]
        public void When_Predict_Is_Called_Should_Forward_Image_Bytes_And_FileName_To_Engine()
        {
            // arrange
            var imageBytes = new byte[] { 9, 8, 7 };
            InMemoryImageData capturedInput = null;
            predictionEnginePoolMock
                .Setup(p => p.Predict(It.IsAny<InMemoryImageData>()))
                .Callback<InMemoryImageData>(input => capturedInput = input)
                .Returns(new ImagePredictionScore { PredictedLabel = "green", Score = new[] { 1f } });

            // act
            sut.Predict(imageBytes, "photo.png");

            // assert
            Assert.NotNull(capturedInput);
            Assert.Same(imageBytes, capturedInput.Image);
            Assert.Equal("photo.png", capturedInput.FileName);
            Assert.Null(capturedInput.Label);
            predictionEnginePoolMock.Verify(p => p.Predict(It.IsAny<InMemoryImageData>()), Times.Once);
        }

        [Fact]
        public void When_Prediction_Score_Is_Empty_Predict_Should_Throw_InvalidOperationException()
        {
            // arrange
            // Pins current behavior: PredictedScore is computed with Score.Max(), which
            // throws for an empty sequence.
            predictionEnginePoolMock
                .Setup(p => p.Predict(It.IsAny<InMemoryImageData>()))
                .Returns(new ImagePredictionScore { PredictedLabel = "red", Score = Array.Empty<float>() });

            // act & assert
            Assert.Throws<InvalidOperationException>(() => sut.Predict(new byte[] { 1 }, "a.png"));
        }

        [Fact]
        public void When_GetModelInfo_Is_Called_Should_Map_Values_From_Model_Info_Service()
        {
            // arrange
            var labels = new[] { "blue", "green", "red" };
            modelInfoServiceMock.SetupGet(m => m.ModelFileName).Returns("image.zip");
            modelInfoServiceMock.SetupGet(m => m.ModelChecksum).Returns("abc123");
            modelInfoServiceMock.SetupGet(m => m.ModelVersion).Returns("20260820.101010");
            modelInfoServiceMock.SetupGet(m => m.Labels).Returns(labels);

            // act
            var modelInfo = sut.GetModelInfo();

            // assert
            Assert.Equal("image.zip", modelInfo.Name);
            Assert.Equal("abc123", modelInfo.Checksum);
            Assert.Equal("20260820.101010", modelInfo.Version);
            Assert.Same(labels, modelInfo.Labels);
        }
    }
}
