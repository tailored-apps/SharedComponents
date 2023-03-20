namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IModelInfoService
    {
        string ModelChecksum { get; }
        string ModelVersion { get; }
        string ModelFilePath { get; }
        string ModelFileName { get; }
        string[] Labels { get; }
    }
}
