using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands
{
    public interface ITrainImageClassificationModelCommandHandler : IRequestHandler<TrainImageClassificationModel, TrainImageClassificationModelResponse>
    {
    }
}
