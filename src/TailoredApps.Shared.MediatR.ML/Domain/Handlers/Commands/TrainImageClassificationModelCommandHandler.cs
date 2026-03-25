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
    /// <summary>
    /// Handles the <see cref="TrainImageClassificationModel"/> MediatR command by loading training images,
    /// training a classification model, and persisting the result with version and label metadata.
    /// </summary>
    public class TrainImageClassificationModelCommandHandler : ITrainImageClassificationModelCommandHandler
    {
        private readonly IImageClassificationService classificationService;
        private readonly IModelHelper modelHelper;

        /// <summary>
        /// Initializes a new instance of <see cref="TrainImageClassificationModelCommandHandler"/>.
        /// </summary>
        /// <param name="classificationService">The service responsible for training the ML model.</param>
        /// <param name="modelHelper">The helper used to embed version and label metadata into the model file.</param>
        public TrainImageClassificationModelCommandHandler(IImageClassificationService classificationService, IModelHelper modelHelper)
        {
            this.classificationService = classificationService;
            this.modelHelper = modelHelper;
        }

        /// <summary>
        /// Processes the <see cref="TrainImageClassificationModel"/> command: loads training images,
        /// trains the model, adds versioning and labels, then returns the training result.
        /// </summary>
        /// <param name="request">
        /// The command containing the source training folder path and the destination model file path.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="TrainImageClassificationModelResponse"/> containing the saved model path,
        /// version, evaluation info, and discovered class labels.
        /// </returns>
        public async Task<TrainImageClassificationModelResponse> Handle(TrainImageClassificationModel request, CancellationToken cancellationToken)
        {
            var response = new TrainImageClassificationModelResponse();

            IEnumerable<ImageData> images = LoadImagesFromDirectory(request.Source);
            var modelInfo = classificationService.Train(images, request.Source,request.ModelDestFolderPath);
            var version = modelHelper.AddVersion(request.ModelDestFolderPath);
            modelHelper.AddLabels(request.ModelDestFolderPath, modelInfo.labels);
            response.ModelPath = request.ModelDestFolderPath;
            response.ModelVersion = version;
            response.ModelInfo = modelInfo.info;
            response.Labels = modelInfo.labels;
            return response;

        }

        /// <summary>
        /// Loads image file paths and labels from a training set directory.
        /// </summary>
        /// <param name="trainingSetFolder">The root directory containing labelled image sub-folders.</param>
        /// <param name="useFolderNameAsLabel">
        /// When <c>true</c> (default), the parent folder name is used as the image label.
        /// </param>
        /// <returns>An enumerable of <see cref="ImageData"/> instances with image paths and labels.</returns>
        private IEnumerable<ImageData> LoadImagesFromDirectory(string trainingSetFolder, bool useFolderNameAsLabel = true)
        {
            return FileUtils.LoadImagesFromDirectory(trainingSetFolder, useFolderNameAsLabel)
                .Select(x => new ImageData(x.ImagePath, x.Label));
        }
    }

    /// <summary>
    /// Represents a labelled image file used as a training sample for the ML model.
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ImageData"/>.
        /// </summary>
        /// <param name="imagePath">The full file path to the image.</param>
        /// <param name="label">The classification label associated with this image.</param>
        public ImageData(string imagePath, string label)
        {
            ImagePath = imagePath;
            Label = label;
        }

        /// <summary>
        /// Gets the full file path to the image.
        /// </summary>
        public string ImagePath { get; }

        /// <summary>
        /// Gets the classification label associated with this image.
        /// </summary>
        public string Label { get; }
    }
}
