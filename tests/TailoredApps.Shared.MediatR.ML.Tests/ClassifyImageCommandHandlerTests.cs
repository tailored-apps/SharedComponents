using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ClassifyImageCommandHandlerTests
    {
        private readonly Mock<IImageClassificationService> classificationServiceMock;
        private readonly ClassifyImageCommandHandler sut;

        public ClassifyImageCommandHandlerTests()
        {
            classificationServiceMock = new Mock<IImageClassificationService>();
            sut = new ClassifyImageCommandHandler(classificationServiceMock.Object);
        }

        [Fact]
        public async Task When_Handle_Receives_Valid_Image_Should_Return_Response_With_Prediction_From_Service()
        {
            // arrange
            var imageBytes = new byte[] { 137, 80, 78, 71, 1, 2, 3 };
            var prediction = new ImagePrediction
            {
                FileName = "testred.png",
                PredictedLabel = "red",
                PredictedScore = 0.98f
            };
            classificationServiceMock
                .Setup(s => s.Predict(imageBytes, "testred.png"))
                .Returns(prediction);
            var request = new ClassifyImage { FileByteArray = imageBytes, FileName = "testred.png" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(response);
            Assert.Same(prediction, response.ImagePrediction);
            Assert.Equal("red", response.ImagePrediction.PredictedLabel);
            Assert.Equal(0.98f, response.ImagePrediction.PredictedScore);
        }

        [Fact]
        public async Task When_Handle_Is_Called_Should_Forward_FileByteArray_And_FileName_To_Service()
        {
            // arrange
            var imageBytes = new byte[] { 255, 216, 255, 224 };
            byte[] forwardedBytes = null;
            string forwardedFileName = null;
            classificationServiceMock
                .Setup(s => s.Predict(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Callback<byte[], string>((bytes, fileName) =>
                {
                    forwardedBytes = bytes;
                    forwardedFileName = fileName;
                })
                .Returns(new ImagePrediction());
            var request = new ClassifyImage { FileByteArray = imageBytes, FileName = "photo.jpg" };

            // act
            await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.Same(imageBytes, forwardedBytes);
            Assert.Equal("photo.jpg", forwardedFileName);
            classificationServiceMock.Verify(s => s.Predict(imageBytes, "photo.jpg"), Times.Once);
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Service_Returns_Null_Prediction_Should_Return_Response_With_Null_Prediction()
        {
            // arrange
            classificationServiceMock
                .Setup(s => s.Predict(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Returns((ImagePrediction)null);
            var request = new ClassifyImage { FileByteArray = new byte[] { 1 }, FileName = "a.png" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(response);
            Assert.Null(response.ImagePrediction);
        }

        [Fact]
        public async Task When_Handle_Receives_Null_Image_Should_Forward_Null_To_Service()
        {
            // arrange
            // Pins current behavior: the handler performs no input validation itself
            // (ImageValidationExtension is not invoked by the handler), so a null byte
            // array is passed straight through to the classification service.
            var prediction = new ImagePrediction();
            classificationServiceMock
                .Setup(s => s.Predict(null, "empty.png"))
                .Returns(prediction);
            var request = new ClassifyImage { FileByteArray = null, FileName = "empty.png" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.Same(prediction, response.ImagePrediction);
            classificationServiceMock.Verify(s => s.Predict(null, "empty.png"), Times.Once);
        }

        [Fact]
        public async Task When_Handle_Receives_Empty_Image_Should_Forward_Empty_Array_To_Service()
        {
            // arrange
            var emptyBytes = Array.Empty<byte>();
            classificationServiceMock
                .Setup(s => s.Predict(emptyBytes, "zero.png"))
                .Returns(new ImagePrediction());
            var request = new ClassifyImage { FileByteArray = emptyBytes, FileName = "zero.png" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(response);
            classificationServiceMock.Verify(s => s.Predict(emptyBytes, "zero.png"), Times.Once);
        }

        [Fact]
        public async Task When_Service_Throws_Should_Propagate_Exception()
        {
            // arrange
            classificationServiceMock
                .Setup(s => s.Predict(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("model not loaded"));
            var request = new ClassifyImage { FileByteArray = new byte[] { 1, 2, 3 }, FileName = "broken.png" };

            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.Handle(request, CancellationToken.None));

            // assert
            Assert.Equal("model not loaded", exception.Message);
        }
    }
}
