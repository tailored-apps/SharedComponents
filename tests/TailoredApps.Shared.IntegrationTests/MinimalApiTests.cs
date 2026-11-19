using System.Net;
using System.Net.Http.Json;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.WebApi.Authentication;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// The minimal-API endpoints of the sample: the same messages as the controllers, with the unit of work,
/// validation and exception handling as endpoint
/// filters (EntityFramework.UnitOfWork.MinimalApi, ExceptionHandling.MinimalApi), against the same database
/// and with the same error model as the controllers.
/// </summary>
public class MinimalApiTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClientAs(Roles.CatalogAdmin);

    [Fact]
    public async Task When_Posting_A_Valid_Product_Should_Commit_It_Although_The_Endpoint_Never_Saves()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new AddProduct { Name = "Matcha", Category = "tea", Price = 89.00m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = (await client.GetFromJsonAsync<GetProductResponse>("/api/minimal/products/Matcha"))?.Product;
        Assert.NotNull(stored);
        Assert.True(stored.Id > 0);
        Assert.Equal(89.00m, stored.Price);

        // The controllers see it too: one database, one unit of work per request.
        var page = await client.GetFromJsonAsync<GetProductsResponse>("/api/products?filter.nameContains=Matcha");
        Assert.Equal(1, page?.Count);
    }

    [Fact]
    public async Task When_The_Body_Is_Invalid_Should_Answer_400_With_The_Fields_And_Store_Nothing()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new AddProduct { Name = string.Empty, Category = "tea", Price = -5m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(400, error.ErrorCode);
        Assert.Contains(error.Errors, e => e.Field == nameof(AddProduct.Name));
        Assert.Contains(error.Errors, e => e.Field == nameof(AddProduct.Price));

        var page = await client.GetFromJsonAsync<GetProductsResponse>("/api/products?filter.category=tea&count=50");
        Assert.DoesNotContain(page!.Results, p => p.Price < 0);
    }

    [Fact]
    public async Task When_The_Product_Already_Exists_Should_Answer_409_And_Store_Nothing()
    {
        var response = await client.PostAsJsonAsync("/api/minimal/products", new AddProduct { Name = "Teapot", Category = "accessories", Price = 1.00m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var page = await client.GetFromJsonAsync<GetProductsResponse>("/api/products?filter.nameContains=Teapot");
        var only = Assert.Single(page!.Results);
        Assert.Equal(129.00m, only.Price);   // the seeded one; the duplicate was refused
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
