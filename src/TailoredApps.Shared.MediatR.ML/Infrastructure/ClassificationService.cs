using System.Linq;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ClassificationService : IClassificationService
    {
        private readonly IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool;
        private readonly IModelInfoService modelInfoService;
        public ClassificationService(
            IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool, IModelInfoService modelInfoService)
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
            ModelInfo modelInfo = GetModelInfo();

            ImagePrediction imagePrediction = new ImagePrediction()
            {
                PredictedScore = prediction.Score.Max(),
                FileName = fileName,
                PredictedLabel = prediction.PredictedLabel,
                ModelInfo = modelInfo,
                Scores = modelInfo.Labels
                    .Zip(prediction.Score, (key, value) => new { key, value })
                    .ToDictionary(x => x.key, x => x.value)

            }
            ;
            return imagePrediction;

        }
    }
}
