using Microsoft.Extensions.Options;
using System.IO;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ModelInfoService : IModelInfoService 
    {
        private readonly IModelHelper modelHelper;
        private readonly IOptions<ImageClassificationOptions> options;
        public ModelInfoService(IOptions<ImageClassificationOptions> options, IModelHelper modelHelper)
        {
            this.modelHelper = modelHelper;
        }
        public string ModelChecksum => modelHelper.GetChecksum(options.Value.ModelFilePath);

        public string ModelVersion => modelHelper.GetVersion(options.Value.ModelFilePath);

        public string ModelFilePath => options.Value.ModelFilePath;

        public string ModelFileName => Path.GetFileName(options.Value.ModelFilePath);

        public string[] Labels => modelHelper.GetLabels(options.Value.ModelFilePath);
    }
}
