using Microsoft.Extensions.Options;
using System.IO;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ModelInfoService : IModelInfoService
    {
        private readonly IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool;
        private readonly IModelHelper modelHelper;
        private readonly IOptions<ImageClassificationOptions> options;
        public ModelInfoService(IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore> predictionEnginePool, IOptions<ImageClassificationOptions> options, IModelHelper modelHelper)
        {
            this.predictionEnginePool = predictionEnginePool;
            this.options = options;
            this.modelHelper = modelHelper;
        }
        public string ModelChecksum => modelHelper.GetChecksum(options.Value.ModelFilePath);

        public string ModelVersion => modelHelper.GetVersion(options.Value.ModelFilePath);

        public string ModelFilePath => options.Value.ModelFilePath;

        public string ModelFileName => Path.GetFileName(options.Value.ModelFilePath);

        public string[] Labels => predictionEnginePool.GetLabels();
    }
}
