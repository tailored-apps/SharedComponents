using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TailoredApps.Shared.WebApi;
using TailoredApps.Shared.WebApi.Options;
using Xunit;

namespace TailoredApps.Shared.WebApi.Tests
{
    public class WebApiDefaultsExtensionsTests
    {
        private static WebApplicationBuilder CreateBuilder()
        {
            return WebApplication.CreateBuilder(Array.Empty<string>());
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Should_Register_Resolved_Options()
        {
            // arrange
            var builder = CreateBuilder();

            // act
            builder.AddTailoredWebApiDefaults(options => options.ServiceName = "test-api");
            using var app = builder.Build();

            // assert
            var options = app.Services.GetRequiredService<WebApiDefaultsOptions>();
            Assert.Equal("test-api", options.ServiceName);
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Should_Register_Health_Checks_With_Self_Liveness_Check()
        {
            // arrange
            var builder = CreateBuilder();

            // act
            builder.AddTailoredWebApiDefaults();
            using var app = builder.Build();

            // assert
            Assert.NotNull(app.Services.GetService<HealthCheckService>());

            var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
            Assert.Contains(registrations, registration => registration.Name == "self" && registration.Tags.Contains("live"));
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Should_Register_OpenTelemetry_Providers()
        {
            // arrange
            var builder = CreateBuilder();

            // act
            builder.AddTailoredWebApiDefaults();
            using var app = builder.Build();

            // assert
            Assert.NotNull(app.Services.GetService<TracerProvider>());
            Assert.NotNull(app.Services.GetService<MeterProvider>());
        }

        [Fact]
        public void AddTailoredWebApiDefaults_When_Health_Checks_Disabled_Should_Not_Register_Health_Check_Service()
        {
            // arrange
            var builder = CreateBuilder();

            // act
            builder.AddTailoredWebApiDefaults(options => options.HealthChecks.Enabled = false);
            using var app = builder.Build();

            // assert
            Assert.Null(app.Services.GetService<HealthCheckService>());
        }

        [Fact]
        public void AddTailoredWebApiDefaults_When_Observability_Disabled_Should_Not_Register_Tracer_Provider()
        {
            // arrange
            var builder = CreateBuilder();

            // act
            builder.AddTailoredWebApiDefaults(options => options.Observability.Enabled = false);
            using var app = builder.Build();

            // assert
            Assert.Null(app.Services.GetService<TracerProvider>());
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Should_Bind_Options_From_Configuration()
        {
            // arrange
            var builder = CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebApiDefaults:ServiceName"] = "configured-api",
                ["WebApiDefaults:HealthChecks:ReadinessPath"] = "/ready",
                ["WebApiDefaults:HttpClient:EnableServiceDiscovery"] = "false"
            });

            // act
            builder.AddTailoredWebApiDefaults();
            using var app = builder.Build();

            // assert
            var options = app.Services.GetRequiredService<WebApiDefaultsOptions>();
            Assert.Equal("configured-api", options.ServiceName);
            Assert.Equal("/ready", options.HealthChecks.ReadinessPath);
            Assert.False(options.HttpClient.EnableServiceDiscovery);
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Configure_Callback_Should_Override_Configuration()
        {
            // arrange
            var builder = CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebApiDefaults:ServiceName"] = "from-config"
            });

            // act
            builder.AddTailoredWebApiDefaults(options => options.ServiceName = "from-code");
            using var app = builder.Build();

            // assert
            var options = app.Services.GetRequiredService<WebApiDefaultsOptions>();
            Assert.Equal("from-code", options.ServiceName);
        }

        [Fact]
        public void MapTailoredWebApiDefaults_Should_Return_Same_Application()
        {
            // arrange
            var builder = CreateBuilder();
            builder.AddTailoredWebApiDefaults();
            using var app = builder.Build();

            // act
            var result = app.MapTailoredWebApiDefaults();

            // assert
            Assert.Same(app, result);
        }

        [Fact]
        public void AddTailoredWebApiDefaults_Should_Throw_When_Builder_Is_Null()
        {
            // arrange
            IHostApplicationBuilder builder = null;

            // act & assert
            Assert.Throws<ArgumentNullException>(() => builder.AddTailoredWebApiDefaults());
        }

        [Fact]
        public void MapTailoredWebApiDefaults_Should_Throw_When_Application_Is_Null()
        {
            // arrange
            WebApplication app = null;

            // act & assert
            Assert.Throws<ArgumentNullException>(() => app.MapTailoredWebApiDefaults());
        }
    }
}
