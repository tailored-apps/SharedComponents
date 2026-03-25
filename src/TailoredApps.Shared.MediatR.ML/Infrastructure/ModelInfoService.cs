using System.IO;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Provides metadata about the currently configured ML model by reading from the model file and configuration.
    /// </summary>
    public class ModelInfoService : IModelInfoService
    {
        private readonly IModelHelper modelHelper;
        private readonly IOptions<ImageClassificationOptions> options;

        /// <summary>
        /// Initializes a new instance of <see cref="ModelInfoService"/>.
        /// </summary>
        /// <param name="options">The configuration options containing the model file path.</param>
        /// <param name="modelHelper">The helper used to extract metadata from the model file.</param>
        public ModelInfoService(IOptions<ImageClassificationOptions> options, IModelHelper modelHelper)
        {
            this.modelHelper = modelHelper;
        }

        /// <summary>
        /// Gets the MD5 checksum of the model file.
        /// </summary>
        public string ModelChecksum => modelHelper.GetChecksum(options.Value.ModelFilePath);

        /// <summary>
        /// Gets the version string embedded in the model file.
        /// </summary>
        public string ModelVersion => modelHelper.GetVersion(options.Value.ModelFilePath);

        /// <summary>
        /// Gets the full file system path to the model file.
        /// </summary>
        public string ModelFilePath => options.Value.ModelFilePath;

        /// <summary>
        /// Gets the file name (without directory) of the model file.
        /// </summary>
        public string ModelFileName => Path.GetFileName(options.Value.ModelFilePath);

        /// <summary>
        /// Gets the array of class label names stored in the model file.
        /// </summary>
        public string[] Labels => modelHelper.GetLabels(options.Value.ModelFilePath);
    }
}
