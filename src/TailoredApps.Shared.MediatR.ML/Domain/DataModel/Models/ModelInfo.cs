namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models
{
    /// <summary>
    /// Represents metadata about a trained ML model, including its name, checksum, version, and class labels.
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ModelInfo"/>.
        /// </summary>
        /// <param name="name">The file name of the model.</param>
        /// <param name="checksum">The MD5 checksum of the model file.</param>
        /// <param name="version">The version string embedded in the model file.</param>
        /// <param name="labels">The array of class label names the model was trained with.</param>
        public ModelInfo(string name, string checksum, string version, string[] labels)
        {
            Labels = labels;
            Version = version;
            Checksum = checksum;
            Name = name;
        }

        /// <summary>
        /// Gets or sets the array of class label names the model was trained to recognise.
        /// </summary>
        public string[] Labels { get; set; }

        /// <summary>
        /// Gets or sets the file name of the model.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version string assigned to the model at training time.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the MD5 checksum of the model file, used for integrity verification.
        /// </summary>
        public string Checksum { get; set; }
    }
}
