namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ImageClassificationOptions
    {
        public const string Position = "ImageClassification";
        public const string ModelFilePathConfig = "ImageClassification:ModelFilePath";
        public string ModelFilePath { get; set; }
    }
}
