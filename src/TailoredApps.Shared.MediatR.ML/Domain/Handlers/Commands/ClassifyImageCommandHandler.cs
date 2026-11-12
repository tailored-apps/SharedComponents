using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Validation;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;

/// <summary>
/// Handles the <see cref="ClassifyImage"/> MediatR command by invoking the image classification service
/// and returning the prediction result.
/// </summary>
/// <remarks>
/// Before the bytes reach the native image decoder the handler checks that the content is present,
/// within <see cref="ImageClassificationOptions.MaxImageBytes"/> and carries a JPEG or PNG signature,
/// so an oversized or malformed upload is rejected with an <see cref="ArgumentException"/> instead of
/// tying up the prediction engine.
/// </remarks>
public class ClassifyImageCommandHandler : IClassifyImageCommandHandler
{
    private readonly IImageClassificationService classificationService;
    private readonly IOptions<ImageClassificationOptions>? options;

    /// <summary>
    /// Initializes a new instance of <see cref="ClassifyImageCommandHandler"/> that applies the
    /// default image size limit. Kept so that code written against earlier 1.x versions keeps working.
    /// </summary>
    /// <param name="classificationService">The image classification service used to run predictions.</param>
    public ClassifyImageCommandHandler(IImageClassificationService classificationService)
        : this(classificationService, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ClassifyImageCommandHandler"/> with explicit options.
    /// </summary>
    /// <param name="classificationService">The service that runs the prediction.</param>
    /// <param name="options">
    /// Image classification options; when <see langword="null"/> the default image size limit applies.
    /// </param>
    public ClassifyImageCommandHandler(IImageClassificationService classificationService, IOptions<ImageClassificationOptions>? options)
    {
        this.classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
        this.options = options;
    }

    /// <summary>
    /// Processes the <see cref="ClassifyImage"/> command and returns the classification result.
    /// </summary>
    /// <param name="request">The command containing the image bytes and file name to classify.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ClassifyImageResponse"/> containing the prediction result for the provided image.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the image is missing, larger than the configured limit, or not a JPEG/PNG file.
    /// </exception>
    public async Task<ClassifyImageResponse> Handle(ClassifyImage request, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var image = ValidateImage(request.FileByteArray);

        return await Task.Run(() =>
        {
            var response = new ClassifyImageResponse { };

            var predictions = classificationService.Predict(image, request.FileName);
            response.ImagePrediction = predictions;

            return response;
        }, cancellationToken);
    }

    private byte[] ValidateImage(byte[]? image)
    {
        if (image == null || image.Length == 0)
        {
            throw new ArgumentException("Image content is required.", nameof(ClassifyImage.FileByteArray));
        }

        var maxBytes = options?.Value?.MaxImageBytes ?? ImageClassificationOptions.DefaultMaxImageBytes;
        if (maxBytes > 0 && image.Length > maxBytes)
        {
            throw new ArgumentException($"Image exceeds the configured limit of {maxBytes} bytes.", nameof(ClassifyImage.FileByteArray));
        }

        if (!image.IsValidImage())
        {
            throw new ArgumentException("Only JPEG and PNG images are supported.", nameof(ClassifyImage.FileByteArray));
        }

        return image;
    }
}
