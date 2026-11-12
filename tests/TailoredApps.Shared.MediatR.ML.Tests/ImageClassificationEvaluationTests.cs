using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests;

/// <summary>
/// The evaluation half of training. Training itself needs the TensorFlow runtime and a downloaded
/// network, so the evaluation is exercised with a small model trained in memory that has the same output
/// columns (<c>LabelAsKey</c>, <c>Score</c>, <c>PredictedLabel</c>).
/// </summary>
public class ImageClassificationEvaluationTests
{
    private sealed class Sample
    {
        public string Label { get; set; }

        [VectorType(2)]
        public float[] Features { get; set; }
    }

    private readonly ImageClassificationService sut = new(
        Mock.Of<IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore>>(),
        Mock.Of<IModelInfoService>());

    /// <summary>Three well separated classes, so the trained model classifies every sample correctly.</summary>
    private static IEnumerable<Sample> Samples()
    {
        for (var i = 0; i < 20; i++)
        {
            var jitter = i / 100f;
            yield return new Sample { Label = "blue", Features = [0f + jitter, 0f + jitter] };
            yield return new Sample { Label = "green", Features = [10f + jitter, 0f + jitter] };
            yield return new Sample { Label = "red", Features = [0f + jitter, 10f + jitter] };
        }
    }

    private static (MLContext Context, IDataView Data, ITransformer Model) TrainSmallModel()
    {
        var context = new MLContext(seed: 1);
        var data = context.Data.LoadFromEnumerable(Samples());
        var pipeline = context.Transforms.Conversion.MapValueToKey("LabelAsKey", "Label", keyOrdinality: Microsoft.ML.Transforms.ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
            .Append(context.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelAsKey", "Features"))
            .Append(context.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));
        return (context, data, pipeline.Fit(data));
    }

    [Fact]
    public void When_Model_Is_Evaluated_Should_Return_Its_Labels_In_Key_Order()
    {
        // arrange
        var (context, data, model) = TrainSmallModel();

        // act
        var (labels, _) = sut.EvaluateModel(context, data, model);

        // assert
        Assert.Equal(new[] { "blue", "green", "red" }, labels);
    }

    [Fact]
    public void When_Model_Is_Evaluated_Should_Describe_Accuracy_And_Log_Loss_Per_Class()
    {
        // arrange
        var (context, data, model) = TrainSmallModel();

        // act
        var (_, info) = sut.EvaluateModel(context, data, model);

        // assert
        var lines = info.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToArray();
        Assert.Equal(6, lines.Length);
        Assert.StartsWith("accuracy macro ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("accuracy micro ", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("LogLoss  ", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("LogLoss for class 1 = ", lines[3], StringComparison.Ordinal);
        Assert.StartsWith("LogLoss for class 2 = ", lines[4], StringComparison.Ordinal);
        Assert.StartsWith("LogLoss for class 3 = ", lines[5], StringComparison.Ordinal);
    }

    [Fact]
    public void When_Schema_Names_The_Score_Slots_Should_Read_Them_As_Labels()
    {
        // arrange
        var (_, data, model) = TrainSmallModel();
        var schema = model.Transform(data).Schema;

        // act
        var labels = sut.GetLabels(schema);

        // assert
        Assert.Equal(new[] { "blue", "green", "red" }, labels);
    }

    [Fact]
    public void When_Schema_Has_No_Score_Column_Should_Throw()
    {
        var schema = new MLContext(seed: 1).Data.LoadFromEnumerable(Samples()).Schema;

        Assert.ThrowsAny<Exception>(() => sut.GetLabels(schema));
    }
}
