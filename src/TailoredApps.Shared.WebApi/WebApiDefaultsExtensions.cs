using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.WebApi.HealthChecks;
using TailoredApps.Shared.WebApi.Http;
using TailoredApps.Shared.WebApi.Observability;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi
{
    /// <summary>
    /// Entry-point extensions that wire a consistent set of production-ready defaults into an ASP.NET Core
    /// Web API: OpenTelemetry (metrics, tracing and logging) exported over OTLP, liveness/readiness health
    /// checks, and resilient, service-discovery-aware HTTP clients.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddTailoredWebApiDefaults();
    ///
    /// var app = builder.Build();
    /// app.MapTailoredWebApiDefaults();
    /// app.Run();
    /// </code>
    /// </example>
    public static class WebApiDefaultsExtensions
    {
        /// <summary>The default configuration section (<c>WebApiDefaults</c>) bound into <see cref="WebApiDefaultsOptions"/>.</summary>
        public const string DefaultConfigurationSectionName = "WebApiDefaults";

        /// <summary>
        /// Registers the Tailored Apps Web API production defaults, binding options from the default
        /// <c>WebApiDefaults</c> configuration section and applying the optional <paramref name="configure"/> overrides.
        /// </summary>
        /// <param name="builder">The host application builder (for example a <see cref="WebApplicationBuilder"/>).</param>
        /// <param name="configure">An optional callback to override the resolved options in code.</param>
        /// <returns>The same <paramref name="builder"/> instance so calls can be chained.</returns>
        public static IHostApplicationBuilder AddTailoredWebApiDefaults(
            this IHostApplicationBuilder builder,
            Action<WebApiDefaultsOptions> configure = null)
        {
            return builder.AddTailoredWebApiDefaults(DefaultConfigurationSectionName, configure);
        }

        /// <summary>
        /// Registers the Tailored Apps Web API production defaults, binding options from the specified
        /// configuration section and applying the optional <paramref name="configure"/> overrides.
        /// </summary>
        /// <param name="builder">The host application builder (for example a <see cref="WebApplicationBuilder"/>).</param>
        /// <param name="configurationSectionName">
        /// The configuration section to bind into <see cref="WebApiDefaultsOptions"/>. Pass <c>null</c> or empty to skip binding.
        /// </param>
        /// <param name="configure">An optional callback to override the resolved options in code. Applied after configuration binding.</param>
        /// <returns>The same <paramref name="builder"/> instance so calls can be chained.</returns>
        public static IHostApplicationBuilder AddTailoredWebApiDefaults(
            this IHostApplicationBuilder builder,
            string configurationSectionName,
            Action<WebApiDefaultsOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new WebApiDefaultsOptions();
            if (!string.IsNullOrWhiteSpace(configurationSectionName))
            {
                builder.Configuration.GetSection(configurationSectionName).Bind(options);
            }

            configure?.Invoke(options);

            // Expose the resolved options to the endpoint-mapping stage and to application code.
            builder.Services.AddSingleton(options);

            builder.AddDefaultOpenTelemetry(options);
            builder.AddDefaultHealthChecks(options);
            builder.AddDefaultHttpClientConfiguration(options);

            return builder;
        }

        /// <summary>
        /// Maps the liveness and readiness health check endpoints configured by
        /// <see cref="AddTailoredWebApiDefaults(IHostApplicationBuilder, Action{WebApiDefaultsOptions})"/>.
        /// Call this after building the <see cref="WebApplication"/>.
        /// </summary>
        /// <param name="app">The built web application.</param>
        /// <returns>The same <paramref name="app"/> instance so calls can be chained.</returns>
        public static WebApplication MapTailoredWebApiDefaults(this WebApplication app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            var options = app.Services.GetService<WebApiDefaultsOptions>() ?? new WebApiDefaultsOptions();

            app.MapDefaultHealthCheckEndpoints(options);

            return app;
        }
    }
}
