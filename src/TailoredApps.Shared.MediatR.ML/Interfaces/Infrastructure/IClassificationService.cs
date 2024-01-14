using System.Collections.Generic;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IImageClassificationService
    {
        ImagePrediction Predict(byte[] image, string fileName);
        ModelInfo GetModelInfo();
        (string info, string[] labels) Train(IEnumerable<ImageData> images, string trainingSetFolder, string modelDestFolderPath);
    }
}
