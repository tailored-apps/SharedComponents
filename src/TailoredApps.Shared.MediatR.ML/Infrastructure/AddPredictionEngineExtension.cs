using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using System;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.MediatR.ML.Infrastructure;
using static TailoredApps.Shared.MediatR.ImageClassification.Infrastructure.ImageClassificationOptions;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public static class AddPredictionEngineExtension
    {

        public static PredictionEnginePoolBuilder<TData, TPrediction> AddAdapter<TData, TPrediction>(this PredictionEnginePoolBuilder<TData, TPrediction> builder)
            where TData : class
            where TPrediction : class, new()
        {

            return builder;

            builder.Services.AddSingleton<IPredictionEnginePoolAdapter<TData, TPrediction>, PredictionEnginePoolAdapter<TData, TPrediction>>();
            return builder;
        }
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
