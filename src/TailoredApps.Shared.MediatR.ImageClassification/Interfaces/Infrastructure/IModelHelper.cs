namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IModelHelper
    {
        string GetChecksum(string modelFilePath);
        string GetVersion(string modelFilePath);
        string AddVersion(string modelFilePath);
    }
}
