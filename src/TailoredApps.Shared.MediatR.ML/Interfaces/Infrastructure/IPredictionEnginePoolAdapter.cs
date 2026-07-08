namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    /// <summary>
    /// Adapter that wraps an ML.NET <c>PredictionEnginePool</c> and exposes a simplified prediction API.
    /// </summary>
    /// <typeparam name="TData">Input data type (feature vector).</typeparam>
    /// <typeparam name="TPrediction">Output prediction type returned by the model.</typeparam>
    public interface IPredictionEnginePoolAdapter<in TData, out TPrediction>
        where TData : class
        where TPrediction : class, new()
    {
        /// <summary>Returns the class labels the underlying model was trained to recognise.</summary>
        string[] GetLabels();

        /// <summary>
        /// Runs the model on a single input example and returns the prediction result.
        /// </summary>
        /// <param name="example">The input feature vector to classify.</param>
        TPrediction Predict(TData example);
    }
}
