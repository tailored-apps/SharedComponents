using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Xunit;

namespace TailoredApps.Shared.WebApi.Tests
{
    /// <summary>
    /// Exercises the resilience defaults through a real <see cref="HttpClient"/> from the factory: the
    /// gateway always answers 503, and the test counts how many times the request was actually sent.
    /// </summary>
    public class HttpClientResilienceTests
    {
        /// <summary>One original attempt plus the standard handler's three retries.</summary>
        private const int AttemptsWhenRetried = 4;

        private sealed class UnavailableGateway : HttpMessageHandler
        {
            private int attempts;

            public int Attempts => attempts;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref attempts);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
        }

        private static async Task<int> CountAttempts(string method, bool retryUnsafeHttpMethods)
        {
            var gateway = new UnavailableGateway();
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.AddTailoredWebApiDefaults(options =>
            {
                options.HttpClient.EnableServiceDiscovery = false;
                options.HttpClient.RetryUnsafeHttpMethods = retryUnsafeHttpMethods;
            });
            builder.Services.AddHttpClient("gateway").ConfigurePrimaryHttpMessageHandler(() => gateway);

            // ConfigureHttpClientDefaults configures an unnamed builder, so the standard pipeline options
            // shared by every client are registered under "<empty name>-standard". The defaults wait
            // seconds between attempts; keep the test fast.
            builder.Services.Configure<HttpStandardResilienceOptions>("-standard", options =>
            {
                options.Retry.Delay = TimeSpan.FromMilliseconds(1);
                options.Retry.UseJitter = false;
            });

            await using var app = builder.Build();
            var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("gateway");

            using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "https://gateway.example/charge"));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            return gateway.Attempts;
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task When_Method_Changes_State_Should_Send_The_Request_Once_By_Default(string method)
        {
            Assert.Equal(1, await CountAttempts(method, retryUnsafeHttpMethods: false));
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public async Task When_Method_Is_Safe_Should_Retry_Transient_Failures(string method)
        {
            Assert.Equal(AttemptsWhenRetried, await CountAttempts(method, retryUnsafeHttpMethods: false));
        }

        [Fact]
        public async Task When_Unsafe_Retries_Are_Opted_In_Should_Retry_Post()
        {
            Assert.Equal(AttemptsWhenRetried, await CountAttempts("POST", retryUnsafeHttpMethods: true));
        }
    }
}
