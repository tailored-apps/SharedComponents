using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ML.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class PredictionEngineServiceConfigurationTests
    {
        [Fact]
        public void When_RegisterMachineLearningModel_Is_Called_Should_Invoke_Builder_Action_With_Builder()
        {
            // arrange
            var services = new ServiceCollection();
            var sut = new PredictionEngineServiceConfiguration(services);
            PredictionEnginePoolBuilder<InMemoryImageData, ImagePredictionScore> capturedBuilder = null;
            var invocationCount = 0;

            // act
            sut.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(builder =>
            {
                capturedBuilder = builder;
                invocationCount++;
            });

            // assert
            Assert.NotNull(capturedBuilder);
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void When_RegisterMachineLearningModel_Is_Called_Should_Register_Prediction_Engine_Pool()
        {
            // arrange
            var services = new ServiceCollection();
            var sut = new PredictionEngineServiceConfiguration(services);

            // act
            sut.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(_ => { });

            // assert
            Assert.Contains(services, d => d.ServiceType == typeof(PredictionEnginePool<InMemoryImageData, ImagePredictionScore>));
        }
    }
}
