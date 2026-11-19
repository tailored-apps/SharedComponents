using ForkForward.Mediator;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Message.Commands;

/// <summary>
/// Mediator command that requests classification of a single image.
/// Returns a <see cref="ClassifyImageResponse"/> containing the prediction result.
/// </summary>
public class ClassifyImage : IRequest<ClassifyImageResponse>
{
    /// <summary>
    /// Gets or sets the raw byte content of the image to classify.
    /// </summary>
    public byte[]? FileByteArray { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the image, used for identification in the response.
    /// </summary>
    public string? FileName { get; set; }
}
