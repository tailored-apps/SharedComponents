using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Handlers.Commands
{
    /// <summary>
    /// MediatR handler contract for the <see cref="ClassifyImage"/> command.
    /// Implementations run the ML classification pipeline and return a <see cref="ClassifyImageResponse"/>.
    /// </summary>
    public interface IClassifyImageCommandHandler : IRequestHandler<ClassifyImage, ClassifyImageResponse>
    {
    }
}
