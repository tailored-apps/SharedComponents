using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Xunit;

namespace TailoredApps.Shared.WebApi.Tests
{
    public class HttpClientResilienceTests
    {
        private static HttpRetryStrategyOptions ResolveRetryOptions(bool retryUnsafeHttpMethods)
        {
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.AddTailoredWebApiDefaults(options =>
            {
                options.HttpClient.EnableServiceDiscovery = false;
                options.HttpClient.RetryUnsafeHttpMethods = retryUnsafeHttpMethods;
            });
            builder.Services.AddHttpClient("payments");
            var app = builder.Build();

            // ConfigureHttpClientDefaults configures an unnamed builder, so the standard pipeline options
            // shared by every client are registered under "<empty name>-standard".
            return app.Services
                .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
                .Get("-standard")
                .Retry;
        }

        private static async Task<bool> ShouldRetry(HttpRetryStrategyOptions retry, HttpMethod method)
        {
            var context = ResilienceContextPool.Shared.Get();
            try
            {
                context.SetRequestMessage(new HttpRequestMessage(method, "https://gateway.example/charge"));
                var outcome = Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                return await retry.ShouldHandle(new RetryPredicateArguments<HttpResponseMessage>(context, outcome, 0));
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PATCH")]
        public async Task When_Method_Is_Not_Idempotent_Should_Not_Retry_By_Default(string method)
        {
            var retry = ResolveRetryOptions(retryUnsafeHttpMethods: false);
            Assert.False(await ShouldRetry(retry, new HttpMethod(method)));
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task When_Method_Is_Idempotent_Should_Still_Retry_Transient_Failures(string method)
        {
            var retry = ResolveRetryOptions(retryUnsafeHttpMethods: false);
            Assert.True(await ShouldRetry(retry, new HttpMethod(method)));
        }

        [Fact]
        public async Task When_Unsafe_Retries_Are_Opted_In_Should_Retry_Post()
        {
            var retry = ResolveRetryOptions(retryUnsafeHttpMethods: true);
            Assert.True(await ShouldRetry(retry, HttpMethod.Post));
        }
    }
}
