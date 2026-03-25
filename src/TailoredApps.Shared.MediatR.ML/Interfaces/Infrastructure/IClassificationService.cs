using System.Collections.Generic;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    /// <summary>
    /// Abstraction for the image classification service that wraps the ML.NET prediction engine.
    /// </summary>
    public interface IImageClassificationService
    {
        /// <summary>
        /// Runs the classification model on the given image bytes and returns the top prediction.
        /// </summary>
        /// <param name="image">Raw image bytes (JPEG, PNG, etc.).</param>
        /// <param name="fileName">Original file name — used for logging and metadata.</param>
        /// <returns>The predicted class label and confidence score.</returns>
        ImagePrediction Predict(byte[] image, string fileName);

        /// <summary>Returns metadata about the currently loaded model (version, checksum, labels).</summary>
        ModelInfo GetModelInfo();

        /// <summary>
        /// Trains a new image classification model from the provided image data set.
        /// </summary>
        /// <param name="images">Collection of labelled training images.</param>
        /// <param name="trainingSetFolder">Path to the folder containing the training image files on disk.</param>
        /// <param name="modelDestFolderPath">Destination folder where the trained model file will be saved.</param>
        /// <returns>
        /// A tuple containing a human-readable training summary string and the list of recognised class labels.
        /// </returns>
        (string info, string[] labels) Train(IEnumerable<ImageData> images, string trainingSetFolder, string modelDestFolderPath);
    }
}
