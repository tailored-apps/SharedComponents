using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;

namespace TailoredApps.Shared.MediatR.ML.Infrastructure
{
    public class PredictionEngineServiceConfiguration
    {
        private IServiceCollection services;

        public PredictionEngineServiceConfiguration(IServiceCollection services)
        {
            this.services = services;
        }

        public void RegisterMachineLearningModel<D, R>(string pathToModel) where D : class where R : class, new()
        {
            services.AddPredictionEnginePool<D, R>()
                .FromFile(pathToModel)
                .AddAdapter();
        }
    }
}
