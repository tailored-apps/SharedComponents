using System;
using Microsoft.AspNetCore.Http;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi.HealthChecks
{
    /// <summary>
    /// Identifies requests to the health check endpoints so tracing can skip them and avoid flooding the
    /// backend with spans from orchestrator probes.
    /// </summary>
    internal static class HealthCheckRequestFilter
    {
        public static bool IsHealthCheckRequest(HttpContext context, WebApiDefaultsOptions options)
        {
            if (context == null || options == null || !options.HealthChecks.Enabled)
            {
                return false;
            }

            var path = context.Request.Path.Value ?? string.Empty;

            return string.Equals(path, options.HealthChecks.ReadinessPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, options.HealthChecks.LivenessPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
