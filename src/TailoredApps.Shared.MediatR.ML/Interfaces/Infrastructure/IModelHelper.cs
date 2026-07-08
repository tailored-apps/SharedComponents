namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    /// <summary>
    /// Provides helpers for reading and writing metadata embedded inside a trained ML.NET model file.
    /// </summary>
    public interface IModelHelper
    {
        /// <summary>Computes and returns the MD5 / SHA checksum of the model file at <paramref name="modelFilePath"/>.</summary>
        /// <param name="modelFilePath">Absolute path to the model file.</param>
        string GetChecksum(string modelFilePath);

        /// <summary>Reads the version tag stored inside the model file.</summary>
        /// <param name="modelFilePath">Absolute path to the model file.</param>
        string GetVersion(string modelFilePath);

        /// <summary>Increments and writes a new version tag into the model file.</summary>
        /// <param name="modelFilePath">Absolute path to the model file.</param>
        /// <returns>The new version string that was written.</returns>
        string AddVersion(string modelFilePath);

        /// <summary>Embeds the provided class labels into the model file for later retrieval.</summary>
        /// <param name="modelFilePath">Absolute path to the model file.</param>
        /// <param name="labels">Array of class label strings to store.</param>
        void AddLabels(string modelFilePath, string[] labels);

        /// <summary>Reads and returns the class labels previously embedded in the model file.</summary>
        /// <param name="modelFilePath">Absolute path to the model file.</param>
        string[] GetLabels(string modelFilePath);
    }
}
