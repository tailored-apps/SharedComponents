using Microsoft.Extensions.ML;
using Microsoft.ML.Data;
using System;
using System.Linq;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Adapts a <see cref="PredictionEnginePool{TData, TPrediction}"/> to the
    /// <see cref="IPredictionEnginePoolAdapter{TData, TPrediction}"/> interface,
    /// providing prediction and label extraction capabilities.
    /// </summary>
    /// <typeparam name="TData">The input data type for the prediction engine.</typeparam>
    /// <typeparam name="TPrediction">The output prediction type produced by the engine.</typeparam>
    public class PredictionEnginePoolAdapter<TData, TPrediction> : IPredictionEnginePoolAdapter<TData, TPrediction>
        where TData : class
        where TPrediction : class, new()
    {
        private readonly PredictionEnginePool<TData, TPrediction> predictionEnginePool;

        /// <summary>
        /// Initializes a new instance of <see cref="PredictionEnginePoolAdapter{TData, TPrediction}"/>.
        /// </summary>
        /// <param name="predictionEngine">The underlying prediction engine pool to wrap.</param>
        public PredictionEnginePoolAdapter(PredictionEnginePool<TData, TPrediction> predictionEngine)
        {
            predictionEnginePool = predictionEngine;
            
        }

        /// <summary>
        /// Retrieves the class label names from the prediction engine's output schema Score slot annotations.
        /// </summary>
        /// <returns>An array of label name strings corresponding to the Score column slots.</returns>
        public string[] GetLabels()
        {
            var labelBuffer = new VBuffer<ReadOnlyMemory<char>>();
            var predictionEngine = predictionEnginePool.GetPredictionEngine();
            predictionEngine.OutputSchema["Score"].Annotations.GetValue("SlotNames", ref labelBuffer);
            return labelBuffer.DenseValues().Select(l => l.ToString()).ToArray();
        }

        /// <summary>
        /// Runs a prediction on the provided input example using the pooled prediction engine.
        /// </summary>
        /// <param name="example">The input data instance to classify.</param>
        /// <returns>The prediction result of type <typeparamref name="TPrediction"/>.</returns>
        public TPrediction Predict(TData example)
        {
            return predictionEnginePool.Predict<TData, TPrediction>(example);
        }
    }
}
