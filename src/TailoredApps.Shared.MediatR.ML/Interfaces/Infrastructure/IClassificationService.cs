using System.Collections.Generic;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IClassificationService
    {
        ImagePrediction Predict(byte[] image, string fileName);
        ModelInfo GetModelInfo();
        string Train(IEnumerable<ImageData> images, string trainingSetFolder, string modelDestFolderPath);
    }
}
