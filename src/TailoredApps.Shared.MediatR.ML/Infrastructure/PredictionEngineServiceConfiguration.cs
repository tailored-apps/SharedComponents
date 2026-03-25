using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using System;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;

namespace TailoredApps.Shared.MediatR.ML.Infrastructure
{
    /// <summary>
    /// Provides configuration support for registering machine learning prediction engine models
    /// with the dependency injection container.
    /// </summary>
    public class PredictionEngineServiceConfiguration
    {
        private IServiceCollection services;

        /// <summary>
        /// Initializes a new instance of <see cref="PredictionEngineServiceConfiguration"/>
        /// with the given service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to register services into.</param>
        public PredictionEngineServiceConfiguration(IServiceCollection services)
        {
            this.services = services;
        }

        /// <summary>
        /// Registers a machine learning model with the prediction engine pool using the provided builder configuration.
        /// </summary>
        /// <typeparam name="D">The input data type used for prediction.</typeparam>
        /// <typeparam name="R">The output result type returned by the prediction engine.</typeparam>
        /// <param name="builder">An action that configures the <see cref="PredictionEnginePoolBuilder{D, R}"/>.</param>
        public void RegisterMachineLearningModel<D, R>(Action<PredictionEnginePoolBuilder<D, R>> builder) where D : class where R : class, new()
        {
            
            var b = services.AddPredictionEnginePool<D, R>().AddAdapter();
            
            builder.Invoke(b);
        }
    }
}
