using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ImageClassificationService : IImageClassificationService

    {
        private readonly IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool;
        private readonly IModelInfoService modelInfoService;
        public ImageClassificationService(IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool, IModelInfoService modelInfoService)
        {
            this.predictionEnginePool = predictionEnginePool;
            this.modelInfoService = modelInfoService;
        }

        public ModelInfo GetModelInfo()
        {
            return new ModelInfo(
                modelInfoService.ModelFileName,
                modelInfoService.ModelChecksum,
                modelInfoService.ModelVersion,
                modelInfoService.Labels
            );
        }

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
            return (res.info,res.labels);
        }
        private (string[] labels, string info)  EvaluateModel(MLContext mlContext, IDataView testDataView, ITransformer trainDataView)
        {
            var watch = Stopwatch.StartNew();
            var predictionDataView = trainDataView.Transform(testDataView);
            var labels = GetLabels(predictionDataView.Schema);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictionDataView, labelColumnName: "LabelAsKey", predictedLabelColumnName: "PredictedLabel");

            watch.Stop();
            var elapsed = watch.ElapsedMilliseconds / 1000;
            return (labels, PrintMultiClassClassificationMetrics("TF DNN:", metrics));
        }

        private string  PrintMultiClassClassificationMetrics(string name, MulticlassClassificationMetrics metrics)
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

        public string[] GetLabels(DataViewSchema schema)
        {
            var labelBuffer = new VBuffer<ReadOnlyMemory<char>>();
            schema["Score"].Annotations.GetValue("SlotNames", ref labelBuffer);
            return labelBuffer.DenseValues().Select(l => l.ToString()).ToArray();
        }

    }
}
