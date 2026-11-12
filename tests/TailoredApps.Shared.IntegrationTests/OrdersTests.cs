using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Demo.Data;
using TailoredApps.Demo.Orders;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// Orders: the unit of work with the transaction filter (EntityFramework.UnitOfWork.WebApiCore), the clock
/// (DateTime), ExceptionHandling, and a payment started at every gateway the demo can fake (Payments and the
/// providers).
/// </summary>
public class OrdersTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task When_Creating_An_Order_Should_Save_It_With_The_Sum_Of_The_Prices_And_The_Current_Time()
    {
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrder("anna@demo.example", [1, 6]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(24.90m + 129.00m, order.Amount);
        Assert.Equal(nameof(OrderStatus.New), order.Status);
        Assert.Equal(DemoApplication.Now, order.CreatedAt);
        Assert.StartsWith("ORD-20261112-", order.Number);

        // Saved and committed by the filter, although the action never called SaveChanges.
        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(order, stored);
        Assert.Equal($"/api/orders/{order.Number}", created.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task When_The_Action_Answers_400_Should_Roll_Back_What_It_Added()
    {
        const string email = "rollback@demo.example";

        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrder(email, [1, 999]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        Assert.False(await db.Orders.AnyAsync(o => o.CustomerEmail == email));
    }

    [Fact]
    public async Task When_Order_Does_Not_Exist_Should_Answer_404()
    {
        var response = await client.GetAsync("/api/orders/ORD-NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Order ORD-NOPE does not exist.", error?.Message);
    }

    [Theory]
    [InlineData("Adyen", null, "adyen")]
    [InlineData("Cashbill", "blik", "cashbill")]
    [InlineData("HotPay", null, "hotpay")]
    [InlineData("PayNow", null, "paynow")]
    [InlineData("PayU", null, "payu")]
    [InlineData("Przelewy24", null, "p24")]
    [InlineData("Revolut", "card", "revolut")]
    [InlineData("Stripe", "card", "stripe")]
    [InlineData("Tpay", null, "tpay")]
    public async Task When_Checking_Out_Should_Return_The_Gateway_Payment_Page_And_Await_Payment(string provider, string? channel, string gateway)
    {
        var order = await CreateOrderAsync($"{provider.ToLowerInvariant()}@demo.example");
        var sentBefore = SentTo(gateway);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.Number}/checkout", new Checkout(provider, channel));

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{provider}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResult>();
        Assert.NotNull(checkout);
        Assert.False(string.IsNullOrEmpty(checkout.RedirectUrl), $"{provider} returned no payment page.");
        Assert.True(Uri.IsWellFormedUriString(checkout.RedirectUrl, UriKind.Absolute), checkout.RedirectUrl);

        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.AwaitingPayment), stored?.Status);
        Assert.Equal(provider, stored?.PaymentProvider);

        // The provider really called its gateway over HTTP (here: the fake), through the registered client.
        Assert.True(SentTo(gateway) > sentBefore, $"{provider} sent nothing to /{gateway}.");
    }

    [Theory]
    [InlineData("Revolut")]   // a provider on HttpClient
    [InlineData("Stripe")]    // the Stripe SDK's StripeException, turned into PaymentGatewayException by the provider
    public async Task When_The_Gateway_Is_Down_Should_Answer_503_And_Leave_The_Order_Unchanged(string provider)
    {
        var order = await CreateOrderAsync($"outage-{provider.ToLowerInvariant()}@demo.example");

        app.Gateways.Unavailable = true;
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync($"/api/orders/{order.Number}/checkout", new Checkout(provider, "card"));
        }
        finally
        {
            app.Gateways.Unavailable = false;
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.New), stored?.Status);
        Assert.Null(stored?.PaymentProvider);
    }

    [Theory]
    [InlineData("Revolut")]
    [InlineData("Stripe")]    // a 4xx StripeException becomes Rejected, as a 4xx answer does for every provider
    public async Task When_The_Gateway_Refuses_Should_Answer_422_And_Leave_The_Order_Unchanged(string provider)
    {
        var order = await CreateOrderAsync($"refused-{provider.ToLowerInvariant()}@demo.example");

        app.Gateways.Refusing = true;
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync($"/api/orders/{order.Number}/checkout", new Checkout(provider, "card"));
        }
        finally
        {
            app.Gateways.Refusing = false;
        }

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.New), stored?.Status);
    }

    private int SentTo(string gateway)
    {
        lock (app.Gateways.Requests)
            return app.Gateways.Requests.Count(r => r == $"POST /{gateway}" || r.StartsWith($"POST /{gateway}/", StringComparison.Ordinal));
    }

    private async Task<OrderDto> CreateOrderAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrder(email, [2, 4]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
