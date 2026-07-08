namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models
{
    /// <summary>
    /// Represents an in-memory image along with its optional label and file name,
    /// used as input to the ML.NET prediction engine.
    /// </summary>
    public class InMemoryImageData
    {
        /// <summary>
        /// Initializes a new instance of <see cref="InMemoryImageData"/>.
        /// </summary>
        /// <param name="image">The raw byte content of the image.</param>
        /// <param name="label">The classification label associated with the image, or <c>null</c> if unknown.</param>
        /// <param name="fileName">The original file name of the image.</param>
        public InMemoryImageData(byte[] image, string label, string fileName)
        {
            Image = image;
            Label = label;
            FileName = fileName;

        }

        /// <summary>
        /// Gets the raw byte content of the image.
        /// </summary>
        public byte[] Image { get; }

        /// <summary>
        /// Gets the classification label associated with the image.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the original file name of the image.
        /// </summary>
        public string FileName { get; }
    }
}
