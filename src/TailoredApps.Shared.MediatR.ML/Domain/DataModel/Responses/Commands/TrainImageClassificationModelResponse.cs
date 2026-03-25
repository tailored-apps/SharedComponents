using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands
{
    /// <summary>
    /// Represents the response returned after handling a <c>TrainImageClassificationModel</c> command,
    /// containing details about the trained model.
    /// </summary>
    public class TrainImageClassificationModelResponse
    {
        /// <summary>
        /// Gets or sets the file system path where the trained model was saved.
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// Gets or sets the version string assigned to the trained model.
        /// </summary>
        public string ModelVersion { get; internal set; }

        /// <summary>
        /// Gets or sets a formatted string containing model evaluation metrics.
        /// </summary>
        public string ModelInfo { get; internal set; }

        /// <summary>
        /// Gets or sets the array of class label names discovered during training.
        /// </summary>
        public string[] Labels { get; internal set; }
    }
}
