
using System.IO;
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
        private readonly IClassificationService classificationService;
        public ClassifyImageCommandHandler(IClassificationService classificationService)
        {
            this.classificationService = classificationService;
        }
        public async Task<ClassifyImageResponse> Handle(ClassifyImage request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var response = new ClassifyImageResponse { };
                byte[] image;
                string fileName;

                image = request.FileByteArray;
                fileName = request.FileName;


                var predictions = classificationService.Predict(image, fileName);
                response.ImagePrediction = predictions;

                return response;
            });
        }
    }
}
