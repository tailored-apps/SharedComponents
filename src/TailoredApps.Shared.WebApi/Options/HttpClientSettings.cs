namespace TailoredApps.Shared.WebApi.Options
{
    /// <summary>
    /// Defaults applied to every <see cref="System.Net.Http.HttpClient"/> created through the
    /// <see cref="System.Net.Http.IHttpClientFactory"/>.
    /// </summary>
    public class HttpClientSettings
    {
        /// <summary>
        /// Adds the standard resilience handler (retries with jittered back-off, circuit breaker,
        /// total-request and per-attempt timeouts, and a concurrency limiter) to all HTTP clients. Default <c>true</c>.
        /// </summary>
        public bool EnableStandardResilience { get; set; } = true;

        /// <summary>
        /// Enables service discovery so HTTP clients can resolve logical service names such as
        /// <c>http://catalog</c> from configuration or a discovery provider. Default <c>true</c>.
        /// </summary>
        public bool EnableServiceDiscovery { get; set; } = true;
    }
}
