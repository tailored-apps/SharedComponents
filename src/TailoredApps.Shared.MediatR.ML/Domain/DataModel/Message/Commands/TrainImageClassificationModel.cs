using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands
{
    /// <summary>
    /// MediatR command that requests training of a new image classification model
    /// from a labelled image dataset. Returns a <see cref="TrainImageClassificationModelResponse"/>
    /// containing the trained model path, version, and evaluation metadata.
    /// </summary>
    public class TrainImageClassificationModel : IRequest<TrainImageClassificationModelResponse>
    {
        /// <summary>
        /// Gets or sets the path to the source directory containing labelled training images.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the destination file path where the trained model will be saved.
        /// </summary>
        public string ModelDestFolderPath { get; set; }
    }
}
