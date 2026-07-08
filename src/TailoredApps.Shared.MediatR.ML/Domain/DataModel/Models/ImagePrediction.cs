
using System.Collections.Generic;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models
{
    /// <summary>
    /// Represents the result of an image classification prediction,
    /// including the predicted label, confidence score, and optional metadata.
    /// </summary>
    public class ImagePrediction
    {
        /// <summary>
        /// Gets or sets the original file name of the classified image.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the label of the class with the highest predicted confidence.
        /// </summary>
        public string PredictedLabel { get; set; }

        /// <summary>
        /// Gets or sets the confidence score of the predicted label (0.0 to 1.0).
        /// </summary>
        public float PredictedScore { get; set; }

        /// <summary>
        /// Gets or sets a dictionary mapping each class label to its confidence score.
        /// </summary>
        public Dictionary<string, float> Scores { get; set; }

        /// <summary>
        /// Gets or sets metadata about the ML model used to produce this prediction.
        /// </summary>
        public ModelInfo ModelInfo { get; set; }
    }
}
