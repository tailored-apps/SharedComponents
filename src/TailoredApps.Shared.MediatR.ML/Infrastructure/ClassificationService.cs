using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Provides image classification and model training functionality using ML.NET.
    /// </summary>
    public class ImageClassificationService : IImageClassificationService

    {
        private readonly IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool;
        private readonly IModelInfoService modelInfoService;

        /// <summary>
        /// Initializes a new instance of <see cref="ImageClassificationService"/>.
        /// </summary>
        /// <param name="predictionEnginePool">The prediction engine pool adapter used to run predictions.</param>
        /// <param name="modelInfoService">The service that provides metadata about the loaded model.</param>
        public ImageClassificationService(IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool, IModelInfoService modelInfoService)
        {
            this.predictionEnginePool = predictionEnginePool;
            this.modelInfoService = modelInfoService;
        }

        /// <summary>
        /// Retrieves metadata about the currently loaded ML model.
        /// </summary>
        /// <returns>A <see cref="ModelInfo"/> object containing the model's name, checksum, version, and labels.</returns>
        public ModelInfo GetModelInfo()
        {
            return new ModelInfo(
                modelInfoService.ModelFileName,
                modelInfoService.ModelChecksum,
                modelInfoService.ModelVersion,
                modelInfoService.Labels
            );
        }

        /// <summary>
        /// Runs an image classification prediction on the provided image bytes.
        /// </summary>
        /// <param name="image">The raw byte content of the image to classify.</param>
        /// <param name="fileName">The original file name of the image, used for identification in the result.</param>
        /// <returns>An <see cref="ImagePrediction"/> containing the predicted label and score.</returns>
        public ImagePrediction Predict(byte[] image, string fileName)
        {
            var imageData = new InMemoryImageData(image, null, fileName);
            ImagePredictionScore prediction = predictionEnginePool.Predict(imageData);
            // ModelInfo modelInfo = GetModelInfo();

            ImagePrediction imagePrediction = new ImagePrediction()
            {
                PredictedScore = prediction.Score.Max(),
                FileName = fileName,
                PredictedLabel = prediction.PredictedLabel,
                //ModelInfo = modelInfo,
                //Scores = modelInfo.Labels
                //    .Zip(prediction.Score, (key, value) => new { key, value })
                //    .ToDictionary(x => x.key, x => x.value)

            }
            ;
            return imagePrediction;

        }

        /// <summary>
        /// Trains an image classification model using the provided image dataset and saves it to disk.
        /// </summary>
        /// <param name="images">The collection of labelled image data used for training.</param>
        /// <param name="trainingSetFolder">The folder path containing the raw training image files.</param>
        /// <param name="modelDestFolderPath">The destination file path where the trained model will be saved.</param>
        /// <returns>
        /// A tuple containing an evaluation info string and an array of class label names discovered during training.
        /// </returns>
        public (string info, string[] labels) Train(IEnumerable<ImageData> images, string trainingSetFolder, string modelDestFolderPath)
        {
            var mlContext = new MLContext(seed: 1);
            IDataView dataView = mlContext.Data.LoadFromEnumerable(images);
            IDataView shuffledImageFileDataset = mlContext.Data.ShuffleRows(dataView);

            IDataView shuffledFullImageDataSet = mlContext.Transforms.Conversion
                .MapValueToKey(outputColumnName: "LabelAsKey", inputColumnName: "Label", keyOrdinality: Microsoft.ML.Transforms.ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
                .Append(mlContext.Transforms.LoadRawImageBytes(
                        outputColumnName: "Image",
                        imageFolder: trainingSetFolder,
                        inputColumnName: "ImagePath"))
                    .Fit(shuffledImageFileDataset)
                    .Transform(shuffledImageFileDataset);

            var trainedTestData = mlContext.Data.TrainTestSplit(shuffledFullImageDataSet, testFraction: 0.2);
            IDataView trainDataView = trainedTestData.TrainSet;
            IDataView testDataView = trainedTestData.TestSet;

            var pipeline = mlContext.MulticlassClassification.Trainers
                .ImageClassification(featureColumnName: "Image", labelColumnName: "LabelAsKey", validationSet: testDataView)
                .Append(mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName: "PredictedLabel", inputColumnName: "PredictedLabel"));

            var watch = Stopwatch.StartNew();

            ITransformer trainedModel = pipeline.Fit(trainDataView);
            watch.Stop();
            var elapsed = watch.ElapsedMilliseconds / 1000;
            var res = EvaluateModel(mlContext, testDataView, trainedModel);
            mlContext.Model.Save(trainedModel, trainDataView.Schema, modelDestFolderPath);
            return (res.info, res.labels);
        }

        /// <summary>
        /// Evaluates the trained model against the test data view and returns evaluation metrics and labels.
        /// </summary>
        /// <param name="mlContext">The ML.NET context used for evaluation.</param>
        /// <param name="testDataView">The data view containing the test dataset.</param>
        /// <param name="trainDataView">The trained transformer to evaluate.</param>
        /// <returns>A tuple of discovered class labels and a formatted metrics info string.</returns>
        private (string[] labels, string info) EvaluateModel(MLContext mlContext, IDataView testDataView, ITransformer trainDataView)
        {
            var watch = Stopwatch.StartNew();
            var predictionDataView = trainDataView.Transform(testDataView);
            var labels = GetLabels(predictionDataView.Schema);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictionDataView, labelColumnName: "LabelAsKey", predictedLabelColumnName: "PredictedLabel");

            watch.Stop();
            var elapsed = watch.ElapsedMilliseconds / 1000;
            return (labels, PrintMultiClassClassificationMetrics("TF DNN:", metrics));
        }

        /// <summary>
        /// Formats multiclass classification metrics into a human-readable string.
        /// </summary>
        /// <param name="name">A label or name prefix for the metrics output.</param>
        /// <param name="metrics">The <see cref="MulticlassClassificationMetrics"/> to format.</param>
        /// <returns>A string containing macro/micro accuracy, log loss, and per-class log loss values.</returns>
        private string PrintMultiClassClassificationMetrics(string name, MulticlassClassificationMetrics metrics)
        {

            var builder = new StringBuilder();

            builder.AppendLine($"accuracy macro {metrics.MacroAccuracy:0.####}, the closer to 1 better");
            builder.AppendLine($"accuracy micro {metrics.MicroAccuracy:0.####}, the closer to 1 better");
            builder.AppendLine($"LogLoss  {metrics.LogLoss:0.####}, the closer to 0 better");
            int i = 0;
            foreach (var classLogLoss in metrics.PerClassLogLoss)
            {
                i++;
                builder.AppendLine($"LogLoss for class {i} = {classLogLoss:0.####}, the closer to 0 better");

            }
            return builder.ToString();
        }

        /// <summary>
        /// Extracts class label names from the Score column's slot name annotations in the data view schema.
        /// </summary>
        /// <param name="schema">The <see cref="DataViewSchema"/> to read label annotations from.</param>
        /// <returns>An array of label name strings.</returns>
        public string[] GetLabels(DataViewSchema schema)
        {
            var labelBuffer = new VBuffer<ReadOnlyMemory<char>>();
            schema["Score"].Annotations.GetValue("SlotNames", ref labelBuffer);
            return labelBuffer.DenseValues().Select(l => l.ToString()).ToArray();
        }

    }
}
