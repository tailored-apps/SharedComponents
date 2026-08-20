using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests
{
    public class ExceptionHandlingOptionsBuilderTests
    {
        private class StubExceptionHandlingProvider : IExceptionHandlingProvider
        {
            public ExceptionHandlingResultModel Response(Exception exception) => null;

            public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => null;
        }

        [Fact]
        public void When_Constructed_Should_Expose_Services()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            var builder = new ExceptionHandlingOptionsBuilder(services);

            // assert
            Assert.Same(services, builder.Services);
        }

        [Fact]
        public void When_WithExceptionHandlingProvider_Is_Called_Should_Register_Transient_Provider()
        {
            // arrange
            var services = new ServiceCollection();
            var builder = new ExceptionHandlingOptionsBuilder(services);

            // act
            var returned = builder.WithExceptionHandlingProvider<StubExceptionHandlingProvider>();

            // assert
            Assert.Same(builder, returned);
            using var serviceProvider = services.BuildServiceProvider();
            var resolved = serviceProvider.GetRequiredService<IExceptionHandlingProvider>();
            Assert.IsType<StubExceptionHandlingProvider>(resolved);
        }

        [Fact]
        public void When_WithExceptionHandlingProvider_Is_Called_With_Factory_Should_Use_Factory_Instance()
        {
            // arrange
            var services = new ServiceCollection();
            var builder = new ExceptionHandlingOptionsBuilder(services);
            var instance = new StubExceptionHandlingProvider();

            // act
            var returned = builder.WithExceptionHandlingProvider(_ => instance);

            // assert
            Assert.Same(builder, returned);
            using var serviceProvider = services.BuildServiceProvider();
            var resolved = serviceProvider.GetRequiredService<IExceptionHandlingProvider>();
            Assert.Same(instance, resolved);
        }
    }
}
