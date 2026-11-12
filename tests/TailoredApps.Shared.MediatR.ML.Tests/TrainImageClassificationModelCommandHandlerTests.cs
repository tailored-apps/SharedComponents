using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
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

            trainingSetFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var options = Options.Create(new ImageClassificationOptions { TrainingRoot = trainingSetFolder, ModelsRoot = trainingSetFolder });
            sut = new TrainImageClassificationModelCommandHandler(classificationServiceMock.Object, modelHelperMock.Object, options);
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
            var missingFolder = Path.Combine(trainingSetFolder, "missing");
            var request = new TrainImageClassificationModel
            {
                Source = missingFolder,
                ModelDestFolderPath = Path.Combine(trainingSetFolder, "model.zip")
            };

            // act & assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Source_Escapes_TrainingRoot_Should_Throw_And_Not_Read_Anything()
        {
            // arrange
            var request = new TrainImageClassificationModel
            {
                Source = Path.Combine(trainingSetFolder, "..", ".."),
                ModelDestFolderPath = Path.Combine(trainingSetFolder, "model.zip")
            };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
            modelHelperMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Destination_Escapes_ModelsRoot_Should_Throw_And_Not_Write_Anything()
        {
            // arrange
            var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
            var request = new TrainImageClassificationModel { Source = trainingSetFolder, ModelDestFolderPath = outside };

            // act & assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
            modelHelperMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Source_Is_Relative_Should_Resolve_Under_TrainingRoot()
        {
            // arrange
            var modelPath = Path.Combine(trainingSetFolder, "model.zip");
            classificationServiceMock
                .Setup(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), Path.Combine(trainingSetFolder, "red"), modelPath))
                .Returns(("info", new[] { "red" }));
            modelHelperMock.Setup(m => m.AddVersion(modelPath)).Returns("v1");
            var request = new TrainImageClassificationModel { Source = "red", ModelDestFolderPath = "model.zip" };

            // act
            var response = await sut.Handle(request, CancellationToken.None);

            // assert
            Assert.Equal(modelPath, response.ModelPath);
            classificationServiceMock.Verify(s => s.Train(It.IsAny<IEnumerable<ImageData>>(), Path.Combine(trainingSetFolder, "red"), modelPath), Times.Once);
        }

        [Fact]
        public async Task When_Roots_Are_Not_Configured_Should_Throw_InvalidOperationException()
        {
            // arrange
            var unconfigured = new TrainImageClassificationModelCommandHandler(
                classificationServiceMock.Object, modelHelperMock.Object, Options.Create(new ImageClassificationOptions()));
            var request = new TrainImageClassificationModel { Source = trainingSetFolder, ModelDestFolderPath = Path.Combine(trainingSetFolder, "m.zip") };

            // act & assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.Handle(request, CancellationToken.None));
            classificationServiceMock.VerifyNoOtherCalls();
        }
    }
}
