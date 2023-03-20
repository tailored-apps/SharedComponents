using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands
{
    public class ClassifyImageResponse
    {
        public ImagePrediction ImagePrediction { get; set; }
    }
}
