using Microsoft.ML.Data;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models
{
    /// <summary>
    /// Represents the raw output of the ML.NET image classification prediction engine,
    /// containing per-class confidence scores and the predicted label.
    /// </summary>
    public class ImagePredictionScore
    {
        /// <summary>
        /// Gets or sets the array of confidence scores for each class, mapped from the <c>Score</c> column.
        /// </summary>
        [ColumnName("Score")]
        public float[] Score { get; set; }

        /// <summary>
        /// Gets or sets the label of the class with the highest predicted confidence score.
        /// </summary>
        public string PredictedLabel { get; set; }
    }
}
