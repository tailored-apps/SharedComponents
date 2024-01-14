using Microsoft.ML.Data;

namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models
{
    public class ImagePredictionScore
    {
        [ColumnName("Score")]
        public float[] Score { get; set; }
        public string PredictedLabel { get; set; }
    }
}
