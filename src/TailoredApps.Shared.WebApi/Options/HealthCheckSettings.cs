namespace TailoredApps.Shared.WebApi.Options
{
    /// <summary>Liveness/readiness health check configuration.</summary>
    public class HealthCheckSettings
    {
        /// <summary>Enables registration and mapping of the health check endpoints. Default <c>true</c>.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Readiness endpoint path. Runs every registered health check and reports whether the
        /// instance is ready to receive traffic. Wire this to an orchestrator readiness probe. Default <c>/health</c>.
        /// </summary>
        public string ReadinessPath { get; set; } = "/health";

        /// <summary>
        /// Liveness endpoint path. Runs only the lightweight checks tagged as "live" (by default the
        /// built-in self check). Wire this to an orchestrator liveness probe. Default <c>/alive</c>.
        /// </summary>
        public string LivenessPath { get; set; } = "/alive";

        /// <summary>
        /// When <c>true</c>, the health responses include a per-check breakdown (name, status, duration
        /// and description). Leave <c>false</c> (default) to expose only the aggregate status and avoid
        /// leaking internal detail on publicly reachable endpoints.
        /// </summary>
        public bool AllowDetailedResponse { get; set; }
    }
}
