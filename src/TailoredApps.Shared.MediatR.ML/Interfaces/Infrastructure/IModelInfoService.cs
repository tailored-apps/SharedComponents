namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    /// <summary>
    /// Exposes metadata about the currently loaded image classification model.
    /// </summary>
    public interface IModelInfoService
    {
        /// <summary>MD5 / SHA checksum of the model file, used for integrity verification.</summary>
        string ModelChecksum { get; }

        /// <summary>Version string embedded in the model file (e.g. "1.3.0").</summary>
        string ModelVersion { get; }

        /// <summary>Absolute path to the model file on disk.</summary>
        string ModelFilePath { get; }

        /// <summary>File name of the model (without directory path).</summary>
        string ModelFileName { get; }

        /// <summary>Array of class labels the model was trained to recognise.</summary>
        string[] Labels { get; }
    }
}
