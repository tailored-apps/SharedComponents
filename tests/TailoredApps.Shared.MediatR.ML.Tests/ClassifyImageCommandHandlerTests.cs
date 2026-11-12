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
            var request = new ClassifyImage { FileByteArray = new byte[] { 137, 80, 78, 71, 1 }, FileName = "a.png" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(response);
            Assert.Null(response.ImagePrediction);
        }

        [Fact]
        public async Task When_Handle_Receives_Null_Image_Should_Reject_Before_Calling_Service()
        {
            // arrange
            var request = new ClassifyImage { FileByteArray = null, FileName = "empty.png" };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Handle_Receives_Empty_Image_Should_Reject_Before_Calling_Service()
        {
            // arrange
            var request = new ClassifyImage { FileByteArray = Array.Empty<byte>(), FileName = "zero.png" };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Handle_Receives_Non_Image_Bytes_Should_Reject_Before_Calling_Service()
        {
            // arrange
            var request = new ClassifyImage { FileByteArray = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, FileName = "evil.png" };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Image_Exceeds_Configured_Limit_Should_Reject_Before_Calling_Service()
        {
            // arrange
            var limited = new ClassifyImageCommandHandler(
                classificationServiceMock.Object,
                Microsoft.Extensions.Options.Options.Create(new TailoredApps.Shared.MediatR.ImageClassification.Infrastructure.ImageClassificationOptions { MaxImageBytes = 8 }));
            var request = new ClassifyImage { FileByteArray = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4, 5, 6 }, FileName = "big.png" };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => limited.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Service_Throws_Should_Propagate_Exception()
        {
            // arrange
            classificationServiceMock
                .Setup(s => s.Predict(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("model not loaded"));
            var request = new ClassifyImage { FileByteArray = new byte[] { 137, 80, 78, 71, 1, 2, 3 }, FileName = "broken.png" };

            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.Handle(request, CancellationToken.None));

            // assert
            Assert.Equal("model not loaded", exception.Message);
        }
    }
}
