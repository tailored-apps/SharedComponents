using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Demo.Catalog;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// The product catalogue: Querying, MediatR.PagedRequest and the EntityFramework paging and sorting for the
/// list; the MediatR pipeline (validation, caching, retry, fallback) and ExceptionHandling for single products.
/// </summary>
public class CatalogTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task When_Listing_Products_Sorted_By_Price_Descending_Should_Return_The_Requested_Page()
    {
        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?page=1&count=3&sortField=Price&sortDir=Desc");

        Assert.NotNull(page);
        Assert.Equal(7, page.Count);
        Assert.Equal(["Teapot", "Dripper", "Espresso beans"], page.Results.Select(p => p.Name));
    }

    [Fact]
    public async Task When_Asking_For_The_Second_Page_Should_Continue_Where_The_First_Ended()
    {
        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?page=2&count=3&sortField=Name&sortDir=Asc");

        Assert.NotNull(page);
        Assert.Equal(["Filter coffee", "Green tea", "Oolong"], page.Results.Select(p => p.Name));
    }

    [Fact]
    public async Task When_Filtering_By_Category_Should_Return_Only_That_Category()
    {
        var page = await client.GetFromJsonAsync<Page<ProductDto>>("/api/products?filter.category=coffee&sortField=Price&sortDir=Asc");

        Assert.NotNull(page);
        Assert.Equal(2, page.Count);
        Assert.All(page.Results, p => Assert.Equal("coffee", p.Category));
        Assert.Equal(["Filter coffee", "Espresso beans"], page.Results.Select(p => p.Name));
    }

    [Fact]
    public async Task When_Sorting_By_A_Field_Outside_The_Allow_List_Should_Answer_400_Naming_The_Field()
    {
        var response = await client.GetAsync("/api/products?sortField=Id&sortDir=Asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Contains(error.Errors, e => e.Field == nameof(GetProducts.SortField));
    }

    [Fact]
    public async Task When_Asking_For_Too_Large_A_Page_Should_Answer_400()
    {
        var response = await client.GetAsync($"/api/products?page=1&count={GetProducts.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task When_Getting_A_Product_Twice_Should_Run_The_Handler_Once()
    {
        var counter = app.Services.GetRequiredService<HandlerCallCounter>();
        var before = counter.GetProduct;

        var first = await client.GetFromJsonAsync<ProductDto>("/api/products/6");
        var second = await client.GetFromJsonAsync<ProductDto>("/api/products/6");

        Assert.Equal("Teapot", first?.Name);
        Assert.Equal(first, second);
        Assert.Equal(before + 1, counter.GetProduct);   // the second answer came from HybridCache
    }

    [Fact]
    public async Task When_Product_Id_Is_Not_Positive_Should_Answer_400_From_The_Validator()
    {
        var response = await client.GetAsync("/api/products/0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(400, error.ErrorCode);
        Assert.Contains(error.Errors, e => e.Field == nameof(GetProduct.Id));
    }

    [Fact]
    public async Task When_Product_Does_Not_Exist_Should_Answer_404_From_The_Exception_Provider()
    {
        var response = await client.GetAsync("/api/products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Product 999 does not exist.", error?.Message);
    }

    [Fact]
    public async Task When_The_Source_Fails_Fewer_Times_Than_The_Retries_Should_Return_Its_Answer()
    {
        var source = app.Services.GetRequiredService<RecommendationSource>();
        source.FailuresLeft = 2;
        var callsBefore = source.Calls;

        var recommendations = await client.GetFromJsonAsync<List<string>>("/api/products/1/recommendations");

        Assert.NotNull(recommendations);
        Assert.Single(recommendations);
        Assert.Equal(callsBefore + 3, source.Calls);   // the first call and two retries
    }

    [Fact]
    public async Task When_The_Source_Keeps_Failing_Should_Answer_With_The_Fallback()
    {
        var source = app.Services.GetRequiredService<RecommendationSource>();
        source.FailuresLeft = 10;
        var callsBefore = source.Calls;

        var response = await client.GetAsync("/api/products/1/recommendations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadFromJsonAsync<List<string>>() ?? ["not read"]);
        Assert.Equal(callsBefore + 3, source.Calls);   // retries ran out, then the fallback answered
        source.FailuresLeft = 0;
    }
}
