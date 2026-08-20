using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class TrainImageClassificationModelCommandHandlerTests : IDisposable
    {
        private readonly Mock<IImageClassificationService> classificationServiceMock;
        private readonly Mock<IModelHelper> modelHelperMock;
        private readonly TrainImageClassificationModelCommandHandler sut;
        private readonly string trainingSetFolder;

        public TrainImageClassificationModelCommandHandlerTests()
        {
            classificationServiceMock = new Mock<IImageClassificationService>();
            modelHelperMock = new Mock<IModelHelper>();
            sut = new TrainImageClassificationModelCommandHandler(classificationServiceMock.Object, modelHelperMock.Object);

            trainingSetFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(trainingSetFolder, "red"));
            Directory.CreateDirectory(Path.Combine(trainingSetFolder, "green"));
            File.WriteAllBytes(Path.Combine(trainingSetFolder, "red", "1.jpg"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(trainingSetFolder, "red", "2.png"), new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(trainingSetFolder, "green", "1.png"), new byte[] { 3 });
            File.WriteAllText(Path.Combine(trainingSetFolder, "green", "notes.txt"), "not an image");
        }

        public void Dispose()
        {
            if (Directory.Exists(trainingSetFolder))
            {
                Directory.Delete(trainingSetFolder, recursive: true);
            }
        }

        [Fact]
        public async Task When_Handle_Is_Called_Should_Return_Response_With_Model_Path_Version_Info_And_Labels()
        {
            // arrange
            var modelPath = Path.Combine(trainingSetFolder, "model.zip");
            var labels = new[] { "green", "red" };
            classificationServiceMock
                .Setup(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), trainingSetFolder, modelPath))
                .Returns(("accuracy macro 1", labels));
            modelHelperMock
                .Setup(m => m.AddVersion(modelPath))
                .Returns("20260820.101010");
            var request = new TrainImageClassificationModel { Source = trainingSetFolder, ModelDestFolderPath = modelPath };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(response);
            Assert.Equal(modelPath, response.ModelPath);
            Assert.Equal("20260820.101010", response.ModelVersion);
            Assert.Equal("accuracy macro 1", response.ModelInfo);
            Assert.Same(labels, response.Labels);
        }

        [Fact]
        public async Task When_Handle_Is_Called_Should_Load_Only_Jpg_And_Png_Images_With_Folder_Name_Labels()
        {
            // arrange
            var modelPath = Path.Combine(trainingSetFolder, "model.zip");
            IEnumerable<ImageData> forwardedImages = null;
            classificationServiceMock
                .Setup(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), trainingSetFolder, modelPath))
                .Callback<IEnumerable<ImageData>, string, string>((images, _, _) => forwardedImages = images)
                .Returns(("info", new[] { "green", "red" }));
            modelHelperMock.Setup(m => m.AddVersion(modelPath)).Returns("v1");
            var request = new TrainImageClassificationModel { Source = trainingSetFolder, ModelDestFolderPath = modelPath };

            // act
            await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.NotNull(forwardedImages);
            var materialized = forwardedImages.OrderBy(i => i.ImagePath).ToList();
            Assert.Equal(3, materialized.Count);
            Assert.All(materialized, i => Assert.True(File.Exists(i.ImagePath)));
            Assert.Contains(materialized, i => Path.GetFileName(i.ImagePath) == "1.jpg" && i.Label == "red");
            Assert.Contains(materialized, i => Path.GetFileName(i.ImagePath) == "2.png" && i.Label == "red");
            Assert.Contains(materialized, i => Path.GetFileName(i.ImagePath) == "1.png" && i.Label == "green");
            Assert.DoesNotContain(materialized, i => Path.GetFileName(i.ImagePath) == "notes.txt");
        }

        [Fact]
        public async Task When_Handle_Is_Called_Should_Delegate_Versioning_And_Labelling_To_Model_Helper()
        {
            // arrange
            var modelPath = Path.Combine(trainingSetFolder, "model.zip");
            var labels = new[] { "green", "red" };
            classificationServiceMock
                .Setup(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), trainingSetFolder, modelPath))
                .Returns(("info", labels));
            modelHelperMock.Setup(m => m.AddVersion(modelPath)).Returns("v1");
            var request = new TrainImageClassificationModel { Source = trainingSetFolder, ModelDestFolderPath = modelPath };

            // act
            await sut.Handle(request, CancellationToken.None);

            // assert
            classificationServiceMock.Verify(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), trainingSetFolder, modelPath), Times.Once);
            modelHelperMock.Verify(m => m.AddVersion(modelPath), Times.Once);
            modelHelperMock.Verify(m => m.AddLabels(modelPath, labels), Times.Once);
            modelHelperMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Source_Directory_Does_Not_Exist_Should_Throw_DirectoryNotFoundException()
        {
            // arrange
            var missingFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var request = new TrainImageClassificationModel
            {
                Source = missingFolder,
                ModelDestFolderPath = Path.Combine(missingFolder, "model.zip")
            };

            // act & assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }
    }
}
