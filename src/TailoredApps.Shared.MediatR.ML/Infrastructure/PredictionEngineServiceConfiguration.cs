using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using System;
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

        public void RegisterMachineLearningModel<D, R>(Action<PredictionEnginePoolBuilder<D, R>> builder) where D : class where R : class, new()
        {
            
            var b = services.AddPredictionEnginePool<D, R>().AddAdapter();
            
            builder.Invoke(b);
        }
    }
}
