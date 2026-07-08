using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi.HealthChecks
{
    /// <summary>Maps the liveness and readiness health check endpoints.</summary>
    internal static class HealthCheckEndpointExtensions
    {
        public static WebApplication MapDefaultHealthCheckEndpoints(this WebApplication app, WebApiDefaultsOptions options)
        {
            var settings = options.HealthChecks;
            if (!settings.Enabled)
            {
                return app;
            }

            var responseWriter = HealthCheckResponseWriter.Create(settings.AllowDetailedResponse);

            // Readiness &#8212; every registered check must pass before the instance receives traffic.
            app.MapHealthChecks(settings.ReadinessPath, new HealthCheckOptions
            {
                ResponseWriter = responseWriter
            });

            // Liveness &#8212; only the lightweight "live" checks; a failure signals the process should be restarted.
            app.MapHealthChecks(settings.LivenessPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(HealthCheckRegistration.LivenessTag),
                ResponseWriter = responseWriter
            });

            return app;
        }
    }
}
