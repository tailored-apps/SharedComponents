namespace TailoredApps.Shared.WebApi.Options
{
    /// <summary>
    /// Root configuration for the Tailored Apps Web API production defaults.
    /// Bind this from the <c>WebApiDefaults</c> configuration section and/or configure it in code.
    /// </summary>
    public class WebApiDefaultsOptions
    {
        /// <summary>
        /// Logical service name reported to OpenTelemetry (the <c>service.name</c> resource attribute).
        /// When left empty the host application name is used.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Service version reported to OpenTelemetry (the <c>service.version</c> resource attribute). Optional.
        /// </summary>
        public string ServiceVersion { get; set; }

        /// <summary>Liveness/readiness health check settings.</summary>
        public HealthCheckSettings HealthChecks { get; set; } = new HealthCheckSettings();

        /// <summary>OpenTelemetry metrics, tracing and logging settings.</summary>
        public ObservabilitySettings Observability { get; set; } = new ObservabilitySettings();

        /// <summary>Outbound <see cref="System.Net.Http.HttpClient"/> resilience and service-discovery settings.</summary>
        public HttpClientSettings HttpClient { get; set; } = new HttpClientSettings();
    }
}
