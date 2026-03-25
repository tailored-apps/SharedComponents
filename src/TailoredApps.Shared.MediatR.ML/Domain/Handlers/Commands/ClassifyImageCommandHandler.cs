using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands
{
    /// <summary>
    /// Handles the <see cref="ClassifyImage"/> MediatR command by invoking the image classification service
    /// and returning the prediction result.
    /// </summary>
    public class ClassifyImageCommandHandler : IClassifyImageCommandHandler
    {
        private readonly IImageClassificationService classificationService;

        /// <summary>
        /// Initializes a new instance of <see cref="ClassifyImageCommandHandler"/>.
        /// </summary>
        /// <param name="classificationService">The image classification service used to run predictions.</param>
        public ClassifyImageCommandHandler(IImageClassificationService classificationService)
        {
            this.classificationService = classificationService;
        }

        /// <summary>
        /// Processes the <see cref="ClassifyImage"/> command and returns the classification result.
        /// </summary>
        /// <param name="request">The command containing the image bytes and file name to classify.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="ClassifyImageResponse"/> containing the prediction result for the provided image.
        /// </returns>
        public async Task<ClassifyImageResponse> Handle(ClassifyImage request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var response = new ClassifyImageResponse { };

                var predictions = classificationService.Predict(request.FileByteArray, request.FileName);
                response.ImagePrediction = predictions;

                return response;
            });
        }
    }
}
