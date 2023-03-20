namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models
{
    public class ModelInfo
    {
        public ModelInfo(string name, string checksum, string version, string[] labels)
        {
            Labels = labels;
            Version = version;
            Checksum = checksum;
            Name = name;
        }
        public string[] Labels { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Checksum { get; set; }
    }
}
