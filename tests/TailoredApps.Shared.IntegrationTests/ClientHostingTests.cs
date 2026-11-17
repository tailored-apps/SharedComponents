using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using Xunit;
using OrderStatus = TailoredApps.Demo.Services.Orders.Entities.OrderStatus;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// The browser client (Demo.Client, Blazor WebAssembly) as the Web API serves it, and the Development-only
/// endpoint the client uses to play the payment gateway. The client itself runs in a browser; what can be
/// checked here is that every file it will ask for is there.
/// </summary>
public partial class ClientHostingTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/basket")]
    [InlineData("/orders/ORD-20261112-abcdef01")]
    public async Task When_Asking_For_A_Page_Of_The_Client_Should_Answer_With_Its_Host_Page(string address)
    {
        var response = await client.GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.webassembly.js", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.css")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.js")]
    [InlineData("/css/app.css")]
    public async Task When_Asking_For_A_File_Of_The_Client_Should_Serve_The_File_Not_The_Page(string address)
    {
        var response = await client.GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task When_The_Client_Starts_Should_Find_Every_Assembly_Its_Boot_Configuration_Names()
    {
        // Blazor names the client's assemblies by their content and lists the names in dotnet.js. A build
        // that left the list and the files out of step shows in a browser only as a page that never loads.
        // Asked the way a browser asks: a browser accepts gzip, and a compressed copy that is older than the
        // file it was made from is exactly how this broke once.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_framework/dotnet.js");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        using var answer = await client.SendAsync(request);
        answer.EnsureSuccessStatusCode();
        await using var body = await answer.Content.ReadAsStreamAsync();
        await using var content = answer.Content.Headers.ContentEncoding.Contains("gzip") ? new GZipStream(body, CompressionMode.Decompress) : body;
        using var reader = new StreamReader(content);
        var boot = await reader.ReadToEndAsync();
        var assemblies = FingerprintedAssembly().Matches(boot).Select(match => match.Groups[1].Value).Distinct().ToList();

        Assert.Contains(assemblies, name => name.StartsWith("TailoredApps.Shared.Demo.Client.", StringComparison.Ordinal));
        Assert.Contains(assemblies, name => name.StartsWith("TailoredApps.Shared.Demo.DataModel.", StringComparison.Ordinal));
        foreach (var assembly in assemblies)
        {
            var response = await client.GetAsync($"/_framework/{assembly}");
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{assembly}: {(int)response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("/api/nothing")]
    [InlineData("/api/products/6/nothing")]
    public async Task When_An_Address_Under_Api_Is_Unknown_Should_Answer_404_Not_The_Page(string address)
    {
        var response = await client.GetAsync(address);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_The_Development_Gateway_Completes_A_Payment_Should_Go_Through_The_Signed_Notification()
    {
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = "played@demo.example", ProductIds = [2, 4] });
        var order = (await created.Content.ReadOrderAsync())!.Number;
        await client.PostAsJsonAsync($"/api/orders/{order}/checkout", new CheckoutOrder { Provider = "Revolut", Channel = "card" });

        var response = await client.PostAsync($"/api/dev/gateway/revolut/orders/{order}/complete", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(OrderStatus.Paid), (await client.GetOrderAsync(order))?.Status);
        Assert.Single(app.Outbox.Messages, message => message.Recipient == "played@demo.example");
    }

    [Fact]
    public async Task When_Not_In_Development_Should_Have_No_Token_And_No_Gateway_Endpoint()
    {
        using var staging = app.WithWebHostBuilder(builder => builder
            .UseEnvironment("Staging")
            .UseSetting("Jwt:Key", "staging-not-a-real-signing-key-0123456789"));
        var stagingClient = staging.CreateClient();

        var token = await stagingClient.PostAsync("/api/dev/token?role=CatalogAdmin", content: null);
        var gateway = await stagingClient.PostAsync("/api/dev/gateway/revolut/orders/ORD-X/complete", content: null);

        Assert.Equal(HttpStatusCode.NotFound, token.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gateway.StatusCode);
    }

    [GeneratedRegex("\"((?:TailoredApps|ForkForward|MudBlazor)[A-Za-z0-9.]*\\.[a-z0-9]{10}\\.wasm)\"")]
    private static partial Regex FingerprintedAssembly();
}
