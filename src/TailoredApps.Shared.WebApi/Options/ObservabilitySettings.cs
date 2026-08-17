using System;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace TailoredApps.Shared.WebApi.Options
{
    /// <summary>OpenTelemetry observability configuration (metrics, tracing and logging).</summary>
    public class ObservabilitySettings
    {
        /// <summary>Master switch for all OpenTelemetry wiring. Default <c>true</c>.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Enables metrics collection (ASP.NET Core, <see cref="System.Net.Http.HttpClient"/> and runtime meters). Default <c>true</c>.</summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>Enables distributed tracing (ASP.NET Core and <see cref="System.Net.Http.HttpClient"/> sources). Default <c>true</c>.</summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>Enables OpenTelemetry logging with formatted messages and scopes. Default <c>true</c>.</summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>Enables .NET runtime metrics (garbage collection, thread pool, exception counts). Default <c>true</c>.</summary>
        public bool EnableRuntimeInstrumentation { get; set; } = true;

        /// <summary>
        /// OTLP exporter endpoint. When empty, the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
        /// environment variable is used. The OTLP exporter is only enabled when an endpoint is resolved,
        /// so telemetry is collected but silently dropped until a collector is configured.
        /// </summary>
        public string OtlpExporterEndpoint { get; set; }

        /// <summary>
        /// Optional hook to further configure the metrics pipeline &#8212; for example to add a Prometheus
        /// exporter and <c>/metrics</c> scrape endpoint, register custom meters, or add views.
        /// </summary>
        public Action<MeterProviderBuilder> ConfigureMetrics { get; set; }

        /// <summary>
        /// Optional hook to further configure the tracing pipeline &#8212; for example to register custom
        /// activity sources, samplers or additional exporters.
        /// </summary>
        public Action<TracerProviderBuilder> ConfigureTracing { get; set; }
    }
}
