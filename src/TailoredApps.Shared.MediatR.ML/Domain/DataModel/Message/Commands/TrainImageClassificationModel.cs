using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands
{
    public class TrainImageClassificationModel : IRequest<TrainImageClassificationModelResponse>
    {
        public string Source { get; set; }
        public string ModelDestFolderPath { get; set; }
    }
}
