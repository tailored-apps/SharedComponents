namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models
{
    public class InMemoryImageData
    {
        public InMemoryImageData(byte[] image, string label, string fileName)
        {
            Image = image;
            Label = label;
            FileName = fileName;

        }

        public byte[] Image { get; }
        public string Label { get; }
        public string FileName { get; }
    }
}
