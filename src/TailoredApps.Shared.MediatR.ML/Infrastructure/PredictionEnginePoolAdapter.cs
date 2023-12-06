using Microsoft.Extensions.ML;
using Microsoft.ML.Data;
using System;
using System.Linq;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class PredictionEnginePoolAdapter<TData, TPrediction> : IPredictionEnginePoolAdapter<TData, TPrediction>
        where TData : class
        where TPrediction : class, new()
    {
        private readonly PredictionEnginePool<TData, TPrediction> predictionEnginePool;

        public PredictionEnginePoolAdapter(PredictionEnginePool<TData, TPrediction> predictionEngine)
        {
            predictionEnginePool = predictionEngine;
        }

        public string[] GetLabels()
        {
            var labelBuffer = new VBuffer<ReadOnlyMemory<char>>();
            var predictionEngine = predictionEnginePool.GetPredictionEngine();
            predictionEngine.OutputSchema["Score"].Annotations.GetValue("SlotNames", ref labelBuffer);
            return labelBuffer.DenseValues().Select(l => l.ToString()).ToArray();
        }

        public TPrediction Predict(TData example)
        {
            return predictionEnginePool.Predict(example);
        }
    }
}
