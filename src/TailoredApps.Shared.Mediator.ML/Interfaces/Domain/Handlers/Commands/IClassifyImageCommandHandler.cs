using ForkForward.Mediator;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.Mediator.ImageClassification.Interfaces.Domain.Handlers.Commands;

/// <summary>
/// Mediator handler contract for the <see cref="ClassifyImage"/> command.
/// Implementations run the ML classification pipeline and return a <see cref="ClassifyImageResponse"/>.
/// </summary>
public interface IClassifyImageCommandHandler : IRequestHandler<ClassifyImage, ClassifyImageResponse>
{
}
