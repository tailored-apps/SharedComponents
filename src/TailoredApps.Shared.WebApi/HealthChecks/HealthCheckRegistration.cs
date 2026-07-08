using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi.HealthChecks
{
    /// <summary>Registers the default health checks used by the liveness and readiness endpoints.</summary>
    internal static class HealthCheckRegistration
    {
        /// <summary>
        /// Tag applied to liveness checks. A liveness check verifies only that the process is up and the
        /// pipeline is responsive; it must never depend on external resources.
        /// </summary>
        public const string LivenessTag = "live";

        public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder, WebApiDefaultsOptions options)
        {
            if (!options.HealthChecks.Enabled)
            {
                return builder;
            }

            builder.Services.AddHealthChecks()
                // The self check anchors the liveness endpoint and always reports healthy once the app is running.
                .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { LivenessTag });

            return builder;
        }
    }
}
