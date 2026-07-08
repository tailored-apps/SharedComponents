using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TailoredApps.Shared.WebApi.HealthChecks;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi.Observability
{
    /// <summary>
    /// Configures the OpenTelemetry metrics, tracing and logging pipelines and, when an endpoint is
    /// available, the OTLP exporter that ships them to a collector.
    /// </summary>
    internal static class OpenTelemetryRegistration
    {
        public static IHostApplicationBuilder AddDefaultOpenTelemetry(this IHostApplicationBuilder builder, WebApiDefaultsOptions options)
        {
            var observability = options.Observability;
            if (!observability.Enabled)
            {
                return builder;
            }

            var serviceName = ResolveServiceName(builder, options);

            if (observability.EnableLogging)
            {
                builder.Logging.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                    logging.SetResourceBuilder(CreateResourceBuilder(serviceName, options.ServiceVersion));
                });
            }

            var openTelemetry = builder.Services.AddOpenTelemetry();
            openTelemetry.ConfigureResource(resource => ConfigureService(resource, serviceName, options.ServiceVersion));

            if (observability.EnableMetrics)
            {
                openTelemetry.WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (observability.EnableRuntimeInstrumentation)
                    {
                        metrics.AddRuntimeInstrumentation();
                    }

                    observability.ConfigureMetrics?.Invoke(metrics);
                });
            }

            if (observability.EnableTracing)
            {
                openTelemetry.WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation(instrumentation =>
                            instrumentation.Filter = context => !HealthCheckRequestFilter.IsHealthCheckRequest(context, options))
                        .AddHttpClientInstrumentation();

                    observability.ConfigureTracing?.Invoke(tracing);
                });
            }

            AddOpenTelemetryExporters(builder, observability);

            return builder;
        }

        private static void AddOpenTelemetryExporters(IHostApplicationBuilder builder, ObservabilitySettings observability)
        {
            // An explicit option wins over the standard environment variable. When neither is present the
            // exporter is left unconfigured: telemetry is still collected in-process but not shipped anywhere.
            if (!string.IsNullOrWhiteSpace(observability.OtlpExporterEndpoint))
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(observability.OtlpExporterEndpoint));
            }
            else if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }
        }

        private static ResourceBuilder CreateResourceBuilder(string serviceName, string serviceVersion)
        {
            return ConfigureService(ResourceBuilder.CreateDefault(), serviceName, serviceVersion);
        }

        private static ResourceBuilder ConfigureService(ResourceBuilder resource, string serviceName, string serviceVersion)
        {
            return resource.AddService(
                serviceName: serviceName,
                serviceVersion: string.IsNullOrWhiteSpace(serviceVersion) ? null : serviceVersion,
                serviceInstanceId: Environment.MachineName);
        }

        private static string ResolveServiceName(IHostApplicationBuilder builder, WebApiDefaultsOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ServiceName))
            {
                return options.ServiceName;
            }

            return string.IsNullOrWhiteSpace(builder.Environment.ApplicationName)
                ? "web-api"
                : builder.Environment.ApplicationName;
        }
    }
}
