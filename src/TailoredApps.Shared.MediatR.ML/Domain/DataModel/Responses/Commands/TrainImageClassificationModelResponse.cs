using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Responses.Commands
{
    public class TrainImageClassificationModelResponse
    {
        public string ModelPath { get; set; }
        public string ModelVersion { get; internal set; }
        public string ModelInfo { get; internal set; }
    }
}

