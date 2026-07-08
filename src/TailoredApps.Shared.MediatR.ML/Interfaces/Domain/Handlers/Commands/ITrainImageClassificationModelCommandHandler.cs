using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands
{
    /// <summary>
    /// MediatR handler contract for the <see cref="TrainImageClassificationModel"/> command.
    /// Implementations train a new ML.NET image classification model and return a <see cref="TrainImageClassificationModelResponse"/>.
    /// </summary>
    public interface ITrainImageClassificationModelCommandHandler : IRequestHandler<TrainImageClassificationModel, TrainImageClassificationModelResponse>
    {
    }
}
