using System.Threading;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands
{
    public class ClassifyImageCommandHandler : IClassifyImageCommandHandler
    {
        private readonly IImageClassificationService classificationService;
        public ClassifyImageCommandHandler(IImageClassificationService classificationService)
        {
            this.classificationService = classificationService;
        }
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
