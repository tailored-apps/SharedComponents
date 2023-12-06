using MediatR;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands
{
    public class ClassifyImage : IRequest<ClassifyImageResponse>
    {
        public byte[] FileByteArray { get; set; }
        public string FileName { get; set; }
    }
}
