using ForkForward.Mediator;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.Mediator.ImageClassification.Interfaces.Domain.Handlers.Commands;

/// <summary>
/// Mediator handler contract for the <see cref="TrainImageClassificationModel"/> command.
/// Implementations train a new ML.NET image classification model and return a <see cref="TrainImageClassificationModelResponse"/>.
/// </summary>
public interface ITrainImageClassificationModelCommandHandler : IRequestHandler<TrainImageClassificationModel, TrainImageClassificationModelResponse>
{
}
