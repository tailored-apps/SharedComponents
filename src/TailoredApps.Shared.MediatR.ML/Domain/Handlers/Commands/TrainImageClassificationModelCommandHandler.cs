using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.MediatR.ML.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands
{
    /// <summary>
    /// Handles the <see cref="TrainImageClassificationModel"/> MediatR command by loading training images,
    /// training a classification model, and persisting the result with version and label metadata.
    /// </summary>
    /// <remarks>
    /// The command's <c>Source</c> and <c>ModelDestFolderPath</c> are resolved against
    /// <see cref="ImageClassificationOptions.TrainingRoot"/> and <see cref="ImageClassificationOptions.ModelsRoot"/>
    /// and rejected when they escape those directories. Without this confinement a request could make the
    /// host recursively read any folder and overwrite any file the process can write.
    /// </remarks>
    public class TrainImageClassificationModelCommandHandler : ITrainImageClassificationModelCommandHandler
    {
        private readonly IImageClassificationService classificationService;
        private readonly IModelHelper modelHelper;
        private readonly IOptions<ImageClassificationOptions> options;

        /// <summary>
        /// Initializes a new instance of <see cref="TrainImageClassificationModelCommandHandler"/>.
        /// </summary>
        /// <param name="classificationService">The service responsible for training the ML model.</param>
        /// <param name="modelHelper">The helper used to embed version and label metadata into the model file.</param>
        /// <param name="options">Options providing the training and model root directories.</param>
        public TrainImageClassificationModelCommandHandler(
            IImageClassificationService classificationService,
            IModelHelper modelHelper,
            IOptions<ImageClassificationOptions> options)
        {
            this.classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
            this.modelHelper = modelHelper ?? throw new ArgumentNullException(nameof(modelHelper));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Processes the <see cref="TrainImageClassificationModel"/> command: loads training images,
        /// trains the model, adds versioning and labels, then returns the training result.
        /// </summary>
        /// <param name="request">
        /// The command containing the source training folder path and the destination model file path,
        /// both relative to (or inside) the configured root directories.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="TrainImageClassificationModelResponse"/> containing the saved model path,
        /// version, evaluation info, and discovered class labels.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the root directories are not configured.</exception>
        /// <exception cref="ArgumentException">Thrown when a path escapes its configured root.</exception>
        public async Task<TrainImageClassificationModelResponse> Handle(TrainImageClassificationModel request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var settings = options.Value ?? new ImageClassificationOptions();
            var source = ResolveUnderRoot(settings.TrainingRoot, request.Source, nameof(ImageClassificationOptions.TrainingRoot), nameof(request.Source));
            var destination = ResolveUnderRoot(settings.ModelsRoot, request.ModelDestFolderPath, nameof(ImageClassificationOptions.ModelsRoot), nameof(request.ModelDestFolderPath));

            cancellationToken.ThrowIfCancellationRequested();

            var response = new TrainImageClassificationModelResponse();

            IEnumerable<ImageData> images = LoadImagesFromDirectory(source);
            var modelInfo = classificationService.Train(images, source, destination);
            var version = modelHelper.AddVersion(destination);
            modelHelper.AddLabels(destination, modelInfo.labels);
            response.ModelPath = destination;
            response.ModelVersion = version;
            response.ModelInfo = modelInfo.info;
            response.Labels = modelInfo.labels;
            return await Task.FromResult(response);
        }

        /// <summary>
        /// Resolves <paramref name="value"/> (relative or absolute) against <paramref name="root"/> and verifies
        /// that the result stays inside the root directory.
        /// </summary>
        internal static string ResolveUnderRoot(string root, string value, string rootSettingName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(
                    $"'{ImageClassificationOptions.ConfigurationKey}:{rootSettingName}' must be configured before models can be trained from a request.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A path is required.", parameterName);
            }

            var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(rootFull, value)));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var inside = candidate.Equals(rootFull, comparison)
                         || candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison);
            if (!inside)
            {
                throw new ArgumentException($"Path must be located inside the configured {rootSettingName}.", parameterName);
            }

            return candidate;
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
