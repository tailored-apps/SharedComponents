using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using System;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.MediatR.ML.Infrastructure;
using static TailoredApps.Shared.MediatR.ImageClassification.Infrastructure.ImageClassificationOptions;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Provides extension methods for configuring the prediction engine and image classification services
    /// in the dependency injection container.
    /// </summary>
    public static class AddPredictionEngineExtension
    {
        /// <summary>
        /// Registers the <see cref="IPredictionEnginePoolAdapter{TData, TPrediction}"/> adapter
        /// for the given prediction engine pool builder.
        /// </summary>
        /// <typeparam name="TData">The input data type for the prediction engine.</typeparam>
        /// <typeparam name="TPrediction">The output prediction type produced by the engine.</typeparam>
        /// <param name="builder">The prediction engine pool builder to extend.</param>
        /// <returns>The original <paramref name="builder"/> instance for chaining.</returns>
        public static PredictionEnginePoolBuilder<TData, TPrediction> AddAdapter<TData, TPrediction>(this PredictionEnginePoolBuilder<TData, TPrediction> builder)
            where TData : class
            where TPrediction : class, new()
        {

            return builder;

            builder.Services.AddSingleton<IPredictionEnginePoolAdapter<TData, TPrediction>, PredictionEnginePoolAdapter<TData, TPrediction>>();
            return builder;
        }

        /// <summary>
        /// Registers all image classification services, model helper, and prediction engine configuration
        /// into the dependency injection container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configuration">
        /// An action to configure the <see cref="PredictionEngineServiceConfiguration"/>,
        /// including model registration.
        /// </param>
        /// <returns>The <paramref name="services"/> instance for chaining.</returns>
        public static IServiceCollection AddPredictionEngine(this IServiceCollection services, Action<PredictionEngineServiceConfiguration> configuration)
        {

            services.ConfigureOptions<ImageClassificationConfigureOptions>();
            var serviceConfig = new PredictionEngineServiceConfiguration(services);
            
            configuration.Invoke(serviceConfig);
            services.AddScoped<IImageClassificationService, ImageClassificationService>();
            services.AddScoped<IModelInfoService, ModelInfoService>();
            services.AddScoped<IModelHelper, ModelHelper>();

            return services;

        }
    }
}
