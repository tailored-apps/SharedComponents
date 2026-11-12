using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using TailoredApps.Shared.WebApi.Options;

namespace TailoredApps.Shared.WebApi.Http
{
    /// <summary>
    /// Applies resilience and service-discovery defaults to every HTTP client created through the
    /// <see cref="System.Net.Http.IHttpClientFactory"/>.
    /// </summary>
    internal static class HttpClientRegistration
    {
        public static IHostApplicationBuilder AddDefaultHttpClientConfiguration(this IHostApplicationBuilder builder, WebApiDefaultsOptions options)
        {
            var settings = options.HttpClient;

            if (settings.EnableServiceDiscovery)
            {
                builder.Services.AddServiceDiscovery();
            }

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                if (settings.EnableStandardResilience)
                {
                    // Turns the transient failure handling on: retries, circuit breaker, timeouts and a concurrency limiter.
                    http.AddStandardResilienceHandler(resilience =>
                    {
                        if (!settings.RetryUnsafeHttpMethods)
                        {
                            // Never replay POST, PUT, PATCH, DELETE or CONNECT automatically: a retried payment or
                            // order request whose first attempt actually reached the server is a duplicate.
                            resilience.Retry.DisableForUnsafeHttpMethods();
                        }
                    });
                }

                if (settings.EnableServiceDiscovery)
                {
                    http.AddServiceDiscovery();
                }
            });

            return builder;
        }
    }
}
