namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models
{
    public class ImagePredictionScore
    {
        public float[] Score { get; set; }
        public string PredictedLabel { get; set; }
    }
}
