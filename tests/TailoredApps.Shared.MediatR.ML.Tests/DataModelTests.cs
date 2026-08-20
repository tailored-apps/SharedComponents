using System.Collections.Generic;
using System.Linq;
using MediatR;
using Microsoft.ML.Data;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ClassifyImageTests
    {
        [Fact]
        public void Should_Round_Trip_Properties()
        {
            // arrange
            var bytes = new byte[] { 1, 2, 3 };

            // act
            var command = new ClassifyImage { FileByteArray = bytes, FileName = "a.png" };

            // assert
            Assert.Same(bytes, command.FileByteArray);
            Assert.Equal("a.png", command.FileName);
        }

        [Fact]
        public void Should_Be_A_MediatR_Request_For_ClassifyImageResponse()
        {
            // arrange & act
            var command = new ClassifyImage();

            // assert
            Assert.IsAssignableFrom<IRequest<ClassifyImageResponse>>(command);
        }
    }

    public class TrainImageClassificationModelTests
    {
        [Fact]
        public void Should_Round_Trip_Properties()
        {
            // arrange & act
            var command = new TrainImageClassificationModel
            {
                Source = @"c:\learning-sets",
                ModelDestFolderPath = @"c:\models\image.zip"
            };

            // assert
            Assert.Equal(@"c:\learning-sets", command.Source);
            Assert.Equal(@"c:\models\image.zip", command.ModelDestFolderPath);
        }

        [Fact]
        public void Should_Be_A_MediatR_Request_For_TrainImageClassificationModelResponse()
        {
            // arrange & act
            var command = new TrainImageClassificationModel();

            // assert
            Assert.IsAssignableFrom<IRequest<TrainImageClassificationModelResponse>>(command);
        }
    }

    public class ClassifyImageResponseTests
    {
        [Fact]
        public void Should_Round_Trip_ImagePrediction()
        {
            // arrange
            var prediction = new ImagePrediction { PredictedLabel = "red" };

            // act
            var response = new ClassifyImageResponse { ImagePrediction = prediction };

            // assert
            Assert.Same(prediction, response.ImagePrediction);
        }
    }

    public class TrainImageClassificationModelResponseTests
    {
        [Fact]
        public void Should_Round_Trip_ModelPath_And_Default_Internal_Properties_To_Null()
        {
            // arrange & act
            // ModelVersion, ModelInfo and Labels have internal setters, so only the
            // handler (same assembly) can assign them; from here only defaults are visible.
            var response = new TrainImageClassificationModelResponse { ModelPath = @"c:\models\image.zip" };

            // assert
            Assert.Equal(@"c:\models\image.zip", response.ModelPath);
            Assert.Null(response.ModelVersion);
            Assert.Null(response.ModelInfo);
            Assert.Null(response.Labels);
        }
    }

    public class ImagePredictionTests
    {
        [Fact]
        public void Should_Round_Trip_Properties()
        {
            // arrange
            var scores = new Dictionary<string, float> { ["red"] = 0.9f, ["green"] = 0.1f };
            var modelInfo = new ModelInfo("image.zip", "abc", "v1", new[] { "green", "red" });

            // act
            var prediction = new ImagePrediction
            {
                FileName = "a.png",
                PredictedLabel = "red",
                PredictedScore = 0.9f,
                Scores = scores,
                ModelInfo = modelInfo
            };

            // assert
            Assert.Equal("a.png", prediction.FileName);
            Assert.Equal("red", prediction.PredictedLabel);
            Assert.Equal(0.9f, prediction.PredictedScore);
            Assert.Same(scores, prediction.Scores);
            Assert.Same(modelInfo, prediction.ModelInfo);
        }
    }

    public class ModelInfoTests
    {
        [Fact]
        public void When_Constructed_Should_Assign_All_Properties()
        {
            // arrange
            var labels = new[] { "blue", "green", "red" };

            // act
            var modelInfo = new ModelInfo("image.zip", "abc123", "20260820.101010", labels);

            // assert
            Assert.Equal("image.zip", modelInfo.Name);
            Assert.Equal("abc123", modelInfo.Checksum);
            Assert.Equal("20260820.101010", modelInfo.Version);
            Assert.Same(labels, modelInfo.Labels);
        }
    }

    public class InMemoryImageDataTests
    {
        [Fact]
        public void When_Constructed_Should_Assign_All_Properties()
        {
            // arrange
            var bytes = new byte[] { 1, 2, 3 };

            // act
            var imageData = new InMemoryImageData(bytes, "red", "a.png");

            // assert
            Assert.Same(bytes, imageData.Image);
            Assert.Equal("red", imageData.Label);
            Assert.Equal("a.png", imageData.FileName);
        }
    }

    public class ImagePredictionScoreTests
    {
        [Fact]
        public void Should_Round_Trip_Properties()
        {
            // arrange
            var score = new[] { 0.2f, 0.8f };

            // act
            var prediction = new ImagePredictionScore { Score = score, PredictedLabel = "red" };

            // assert
            Assert.Same(score, prediction.Score);
            Assert.Equal("red", prediction.PredictedLabel);
        }

        [Fact]
        public void Score_Property_Should_Be_Mapped_To_Score_Column()
        {
            // arrange
            var property = typeof(ImagePredictionScore).GetProperty(nameof(ImagePredictionScore.Score));

            // act
            var attribute = property.CustomAttributes.Single(a => a.AttributeType == typeof(ColumnNameAttribute));

            // assert
            Assert.Equal("Score", (string)attribute.ConstructorArguments.Single().Value);
        }
    }

    public class ImageDataTests
    {
        [Fact]
        public void When_Constructed_Should_Assign_All_Properties()
        {
            // arrange & act
            var imageData = new ImageData(@"c:\learning-sets\red\1.png", "red");

            // assert
            Assert.Equal(@"c:\learning-sets\red\1.png", imageData.ImagePath);
            Assert.Equal("red", imageData.Label);
        }
    }
}
