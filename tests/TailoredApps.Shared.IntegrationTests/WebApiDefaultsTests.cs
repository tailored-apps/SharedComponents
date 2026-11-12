using System.Net;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>WebApi: the health endpoints that AddTailoredWebApiDefaults and MapTailoredWebApiDefaults add.</summary>
public class WebApiDefaultsTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task When_Probing_Health_Endpoint_Should_Answer_Healthy(string path)
    {
        var response = await app.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}
