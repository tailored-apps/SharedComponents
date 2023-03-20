using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IClassificationService
    {
        ImagePrediction Predict(byte[] image, string fileName);
        ModelInfo GetModelInfo();

    }
}
