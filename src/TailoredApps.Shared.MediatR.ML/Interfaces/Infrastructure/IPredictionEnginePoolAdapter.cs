namespace TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure
{
    public interface IPredictionEnginePoolAdapter<in TData, out TPrediction>
        where TData : class
        where TPrediction : class, new()

    {
        string[] GetLabels();
        TPrediction Predict(TData example);

    }
}
