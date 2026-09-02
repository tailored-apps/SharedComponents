using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
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
                            // Never replay POST/PATCH etc. automatically: a retried payment or order
                            // request whose first attempt actually reached the server is a duplicate.
                            DisableRetryForUnsafeHttpMethods(resilience.Retry);
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

        /// <summary>
        /// Wraps the retry predicate so that requests whose HTTP method is not idempotent
        /// (anything other than GET, HEAD, OPTIONS, TRACE, PUT, DELETE) are never retried.
        /// </summary>
        internal static void DisableRetryForUnsafeHttpMethods(HttpRetryStrategyOptions retry)
        {
            var inner = retry.ShouldHandle;
            retry.ShouldHandle = args =>
            {
                var method = args.Context.GetRequestMessage()?.Method;
                if (method is not null && !IsIdempotent(method))
                {
                    return new ValueTask<bool>(false);
                }

                return inner(args);
            };
        }

        /// <summary>Returns <c>true</c> for HTTP methods defined as idempotent by RFC 9110.</summary>
        internal static bool IsIdempotent(HttpMethod method)
            => method == HttpMethod.Get
               || method == HttpMethod.Head
               || method == HttpMethod.Options
               || method == HttpMethod.Trace
               || method == HttpMethod.Put
               || method == HttpMethod.Delete;
    }
}
