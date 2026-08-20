using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.MediatR.ML.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class AddPredictionEngineExtensionTests
    {
        [Fact]
        public void When_AddPredictionEngine_Is_Called_Should_Return_Same_Service_Collection()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            var result = services.AddPredictionEngine(_ => { });

            // assert
            Assert.Same(services, result);
        }

        [Fact]
        public void When_AddPredictionEngine_Is_Called_Should_Invoke_Configuration_Action_With_Service_Configuration()
        {
            // arrange
            var services = new ServiceCollection();
            PredictionEngineServiceConfiguration capturedConfiguration = null;

            // act
            services.AddPredictionEngine(configuration => capturedConfiguration = configuration);

            // assert
            Assert.NotNull(capturedConfiguration);
        }

        [Fact]
        public void When_AddPredictionEngine_Is_Called_Should_Register_Scoped_Image_Classification_Services()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddPredictionEngine(_ => { });

            // assert
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IImageClassificationService)
                && d.ImplementationType == typeof(ImageClassificationService)
                && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IModelInfoService)
                && d.ImplementationType == typeof(ModelInfoService)
                && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IModelHelper)
                && d.ImplementationType == typeof(ModelHelper)
                && d.Lifetime == ServiceLifetime.Scoped);
        }

        [Fact]
        public void When_AddPredictionEngine_Is_Called_Should_Register_Options_Configurator()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddPredictionEngine(_ => { });

            // assert
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<ImageClassificationOptions>)
                && d.ImplementationType == typeof(ImageClassificationOptions.ImageClassificationConfigureOptions));
        }

        [Fact]
        public void When_AddPredictionEngine_Is_Called_Should_Resolve_Model_Helper_From_Provider()
        {
            // arrange
            var services = new ServiceCollection();
            services.AddPredictionEngine(_ => { });

            // act
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var modelHelper = scope.ServiceProvider.GetRequiredService<IModelHelper>();

            // assert
            Assert.IsType<ModelHelper>(modelHelper);
        }

        [Fact]
        public void When_AddAdapter_Is_Called_Should_Register_Singleton_Adapter_And_Return_Same_Builder()
        {
            // arrange
            var services = new ServiceCollection();
            var builder = services.AddPredictionEnginePool<InMemoryImageData, ImagePredictionScore>();

            // act
            var result = builder.AddAdapter();

            // assert
            Assert.Same(builder, result);
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IPredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore>)
                && d.ImplementationType == typeof(PredictionEnginePoolAdapter<InMemoryImageData, ImagePredictionScore>)
                && d.Lifetime == ServiceLifetime.Singleton);
        }
    }
}
