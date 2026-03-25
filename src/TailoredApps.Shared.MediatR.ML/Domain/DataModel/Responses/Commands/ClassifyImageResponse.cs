using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands
{
    /// <summary>
    /// Represents the response returned after handling a <c>ClassifyImage</c> command,
    /// containing the classification prediction result.
    /// </summary>
    public class ClassifyImageResponse
    {
        /// <summary>
        /// Gets or sets the image prediction result, including the predicted label and confidence score.
        /// </summary>
        public ImagePrediction ImagePrediction { get; set; }
    }
}
