using System;
using System.Linq;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.DI;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class PipelineRegistrationTests
    {
        [Fact]
        public void Should_Register_All_Five_Pipeline_Behaviors_As_Transient_In_Order()
        {
            // arrange
            var services = new ServiceCollection();
            var sut = new PipelineRegistration(services);

            // act
            sut.RegisterPipelineBehaviors();

            // assert
            var descriptors = services.Where(d => d.ServiceType == typeof(IPipelineBehavior<,>)).ToList();
            Assert.Equal(5, descriptors.Count);
            Assert.All(descriptors, d => Assert.Equal(ServiceLifetime.Transient, d.Lifetime));
            Assert.Equal(
                new[]
                {
                    typeof(LoggingBehavior<,>),
                    typeof(ValidationBehavior<,>),
                    typeof(CachingBehavior<,>),
                    typeof(FallbackBehavior<,>),
                    typeof(RetryBehavior<,>),
                },
                descriptors.Select(d => d.ImplementationType).ToArray());
        }

        [Fact]
        public void Should_Resolve_All_Behaviors_For_A_Request_In_Registration_Order()
        {
            // arrange
            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<ICache>(Mock.Of<ICache>());
            new PipelineRegistration(services).RegisterPipelineBehaviors();

            // act
            using var provider = services.BuildServiceProvider();
            var behaviors = provider.GetServices<IPipelineBehavior<CachedRequest, string>>().ToList();

            // assert
            Assert.Equal(5, behaviors.Count);
            Assert.IsType<LoggingBehavior<CachedRequest, string>>(behaviors[0]);
            Assert.IsType<ValidationBehavior<CachedRequest, string>>(behaviors[1]);
            Assert.IsType<CachingBehavior<CachedRequest, string>>(behaviors[2]);
            Assert.IsType<FallbackBehavior<CachedRequest, string>>(behaviors[3]);
            Assert.IsType<RetryBehavior<CachedRequest, string>>(behaviors[4]);
        }

        [Fact]
        public void When_Registering_With_Assembly_Should_Scan_And_Register_Policies_And_Handlers_As_Transient()
        {
            // arrange
            var services = new ServiceCollection();
            var sut = new PipelineRegistration(services);

            // act
            sut.RegisterPipelineBehaviors(typeof(PipelineRegistrationTests).Assembly);

            // assert
            Assert.Contains(
                services,
                d => d.ServiceType == typeof(ICachePolicy<CachedRequest, string>)
                    && d.ImplementationType == typeof(DefaultCachePolicy)
                    && d.Lifetime == ServiceLifetime.Transient);
            Assert.Contains(
                services,
                d => d.ServiceType == typeof(IFallbackHandler<FallbackRequest, string>)
                    && d.ImplementationType == typeof(RecoveringFallbackHandler)
                    && d.Lifetime == ServiceLifetime.Transient);
            Assert.Contains(
                services,
                d => d.ServiceType == typeof(IRetryableRequest<RetryRequest, string>)
                    && d.Lifetime == ServiceLifetime.Transient);
        }

        [Fact]
        public void When_Registering_With_Assembly_Should_Not_Add_Pipeline_Behaviors()
        {
            // arrange
            var services = new ServiceCollection();
            var sut = new PipelineRegistration(services);

            // act
            sut.RegisterPipelineBehaviors(typeof(PipelineRegistrationTests).Assembly);

            // assert - pins current behavior: the assembly overload only scans for policies/handlers;
            // callers must also invoke the parameterless overload to get the five behaviors registered.
            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IPipelineBehavior<,>));
        }
    }
}
