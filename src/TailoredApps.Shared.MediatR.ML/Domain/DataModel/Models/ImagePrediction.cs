
using System.Collections.Generic;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Models
{
    public class ImagePrediction
    {
        public string FileName { get; set; }
        public string PredictedLabel { get; set; }
        public float PredictedScore { get; set; }
        public Dictionary<string, float> Scores { get; set; }
        public ModelInfo ModelInfo { get; set; }
    }
}
