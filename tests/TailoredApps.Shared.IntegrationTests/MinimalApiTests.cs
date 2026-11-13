using System.Net;
using System.Net.Http.Json;
using TailoredApps.Demo.Catalog;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// The minimal-API endpoints of the demo: the unit of work, validation and exception handling as endpoint
/// filters (EntityFramework.UnitOfWork.MinimalApi, ExceptionHandling.MinimalApi), against the same database
/// and with the same error model as the controllers.
/// </summary>
public class MinimalApiTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task When_Posting_A_Valid_Product_Should_Commit_It_Although_The_Endpoint_Never_Saves()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new NewProduct("Matcha", "tea", 89.00m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = await client.GetFromJsonAsync<ProductDto>("/api/minimal/products/Matcha");
        Assert.NotNull(stored);
        Assert.True(stored.Id > 0);
        Assert.Equal(89.00m, stored.Price);

        // The controllers see it too: one database, one unit of work per request.
        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?filter.nameContains=Matcha");
        Assert.Equal(1, page?.Count);
    }

    [Fact]
    public async Task When_The_Body_Is_Invalid_Should_Answer_400_With_The_Fields_And_Store_Nothing()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new NewProduct(string.Empty, "tea", -5m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(400, error.ErrorCode);
        Assert.Contains(error.Errors, e => e.Field == nameof(NewProduct.Name));
        Assert.Contains(error.Errors, e => e.Field == nameof(NewProduct.Price));

        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?filter.category=tea&count=50");
        Assert.DoesNotContain(page!.Results, p => p.Price < 0);
    }

    [Fact]
    public async Task When_The_Endpoint_Answers_409_Should_Roll_Back_What_It_Added()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new NewProduct("Teapot", "accessories", 1.00m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?filter.nameContains=Teapot");
        var only = Assert.Single(page!.Results);
        Assert.Equal(129.00m, only.Price);   // the seeded one; the duplicate was rolled back
    }

    [Fact]
    public async Task When_The_Endpoint_Throws_Should_Answer_With_The_Error_Model_Of_The_Provider()
    {
        var response = await client.GetAsync("/api/minimal/products/Nothing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Product 'Nothing' does not exist.", error?.Message);
    }
}
