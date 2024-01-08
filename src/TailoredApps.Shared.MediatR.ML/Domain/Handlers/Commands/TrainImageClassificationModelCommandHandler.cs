using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.MediatR.ML.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands
{
    public class TrainImageClassificationModelCommandHandler : ITrainImageClassificationModelCommandHandler
    {
        private readonly IClassificationService classificationService;
        private readonly IModelHelper modelHelper;
        public TrainImageClassificationModelCommandHandler(IClassificationService classificationService, IModelHelper modelHelper)
        {
            this.classificationService = classificationService;
            this.modelHelper = modelHelper;
        }
        public async Task<TrainImageClassificationModelResponse> Handle(TrainImageClassificationModel request, CancellationToken cancellationToken)
        {
            var response = new TrainImageClassificationModelResponse();

            IEnumerable<ImageData> images = LoadImagesFromDirectory(request.Source);
            var modelInfo = classificationService.Train(images, request.Source,request.ModelDestFolderPath);
            var version = modelHelper.AddVersion(request.ModelDestFolderPath);
            response.ModelPath = request.ModelDestFolderPath;
            response.ModelVersion = version;
            response.ModelInfo = modelInfo;
            return response;

        }

        private IEnumerable<ImageData> LoadImagesFromDirectory(string trainingSetFolder, bool useFolderNameAsLabel = true)
        {
            return FileUtils.LoadImagesFromDirectory(trainingSetFolder, useFolderNameAsLabel)
                .Select(x => new ImageData(x.ImagePath, x.Label));
        }
    }

    public class ImageData
    {
        public ImageData(string imagePath, string label)
        {
            ImagePath = imagePath;
            Label = label;
        }
        public string ImagePath { get; }
        public string Label { get; }
    }
}
