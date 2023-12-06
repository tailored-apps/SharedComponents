using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public static class AddPredictionEngineExtension
    {
        public static PredictionEnginePoolBuilder<TData, TPrediction> AddAdapter<TData, TPrediction>(this PredictionEnginePoolBuilder<TData, TPrediction> builder)
            where TData : class
            where TPrediction : class, new()
        {
            builder.Services.AddSingleton<IPredictionEnginePoolAdapter<TData, TPrediction>, PredictionEnginePoolAdapter<TData, TPrediction>>();
            return builder;
        }
        public static IServiceCollection AddPredictionEngine(this IServiceCollection services, IConfiguration configuration)
        {

            services.ConfigureOptions<Office365EmailConfigureOptions>();
            services.AddPredictionEnginePool<InMemoryImageData, ImagePredictionScore>()
                .FromFile(configuration[ImageClassificationOptions.ModelFilePathConfig])
                .AddAdapter();
            services.AddScoped<IClassificationService, ClassificationService>();
            services.AddScoped<IModelInfoService, ModelInfoService>();
            services.AddScoped<IModelHelper, ModelHelper>();

            return services;

        }
    }
}
