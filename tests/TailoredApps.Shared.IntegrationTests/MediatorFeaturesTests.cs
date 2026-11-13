using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ForkForward.Mapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Demo.WebApi.Authentication;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.Models;
using Xunit;
using OrderStatus = TailoredApps.Demo.Services.Orders.Entities.OrderStatus;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// What the sample shows of the mediator and the mapper beyond a request with a response: a request without
/// one, a notification with two handlers, a stream with its behavior, pre- and post-processors, an exception
/// action and an exception handler, mapping by convention and projection - and the roles on the endpoints.
/// </summary>
public class MediatorFeaturesTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    private ShopMetrics Metrics => app.Services.GetRequiredService<ShopMetrics>();

    // ─── Authorization ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/minimal/products")]
    public async Task When_Adding_A_Product_Without_A_Token_Should_Answer_401(string address)
    {
        var response = await client.PostAsJsonAsync(address, new AddProduct { Name = "Anonymous tea", Category = "tea", Price = 10m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/minimal/products")]
    public async Task When_Adding_A_Product_Without_The_Role_Should_Answer_403(string address)
    {
        var response = await app.CreateClientAs("Customer").PostAsJsonAsync(address, new AddProduct { Name = "Customer tea", Category = "tea", Price = 10m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task When_A_Token_Is_Signed_With_Another_Key_Should_Answer_401()
    {
        var options = new JwtOptions { Key = "another-key-another-key-another-key-0123" };
        var forged = app.CreateClient();
        forged.DefaultRequestHeaders.Authorization = new("Bearer", DevelopmentTokens.Create(options, "intruder", Roles.CatalogAdmin));

        var response = await forged.PostAsJsonAsync("/api/products", new AddProduct { Name = "Forged tea", Category = "tea", Price = 10m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_A_Catalog_Admin_Adds_A_Product_Should_Answer_201_With_The_Key_The_Database_Gave()
    {
        var response = await app.CreateClientAs(Roles.CatalogAdmin).PostAsJsonAsync("/api/products", new AddProduct { Name = "Sencha", Category = "tea", Price = 31.50m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<AddProductResponse>())?.Product;
        Assert.NotNull(added);
        Assert.True(added.Id > 0);
        Assert.EndsWith($"/api/products/{added.Id}", response.Headers.Location?.OriginalString);

        var stored = await client.GetFromJsonAsync<GetProductResponse>($"/api/products/{added.Id}");
        Assert.Equivalent(added, stored?.Product);
    }

    [Fact]
    public async Task When_The_Product_Already_Exists_Should_Answer_409_With_The_Message_Of_The_Service()
    {
        var response = await app.CreateClientAs(Roles.CatalogAdmin).PostAsJsonAsync("/api/products", new AddProduct { Name = "Oolong", Category = "tea", Price = 1m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(409, error?.ErrorCode);
        Assert.Equal("Product 'Oolong' already exists.", error?.Message);
    }

    // ─── A stream request and its behavior ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_Exporting_A_Category_Should_Stream_Its_Products_And_Count_Each_Once()
    {
        var before = Metrics.ProductsExported;

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products/export?category=coffee");

        Assert.Equal(["Espresso beans", "Filter coffee"], products?.Select(product => product.Name));
        Assert.Equal(before + 2, Metrics.ProductsExported);   // the stream behavior saw every item, once
    }

    // ─── Mapping ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void When_The_Mapping_Profiles_Are_Checked_Should_Have_No_Unmapped_Member()
        => app.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public async Task When_An_Order_Repeats_A_Product_Should_Map_One_Line_With_The_Quantity_The_Name_And_The_Total()
    {
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = "lines@demo.example", ProductIds = [2, 2, 6] });

        var order = await created.Content.ReadOrderAsync();
        Assert.NotNull(order);
        Assert.Equal(2 * 19.50m + 129.00m, order.Amount);

        var stored = await client.GetOrderAsync(order.Number);
        var tea = Assert.Single(stored!.Lines, line => line.ProductId == 2);
        Assert.Equal("Black tea", tea.ProductName);   // flattened from Product.Name
        Assert.Equal(2, tea.Quantity);
        Assert.Equal(39.00m, tea.LineTotal);          // computed by the profile
        Assert.Equal(2, stored.Lines.Count);
    }

    // ─── A pre-processor ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_The_Address_Has_Spaces_And_Capitals_Should_Store_It_Normalised()
    {
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = "  Anna.Nowak@Demo.Example ", ProductIds = [1] });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("anna.nowak@demo.example", (await created.Content.ReadOrderAsync())?.CustomerEmail);
    }

    [Fact]
    public async Task When_The_Address_Is_Not_An_Address_Should_Answer_400_Naming_The_Field()
    {
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = "not-an-address", ProductIds = [1] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(error!.Errors, e => e.Field == nameof(CreateOrder.CustomerEmail));
    }

    // ─── A post-processor and an exception action ───────────────────────────────────────────────────────

    [Fact]
    public async Task When_A_Payment_Starts_Should_Count_It_After_The_Handler()
    {
        var order = await CreateOrderAsync("counted@demo.example");
        var (started, outages) = (Metrics.PaymentsStarted, Metrics.GatewayOutages);

        var response = await client.PostAsJsonAsync($"/api/orders/{order}/checkout", new CheckoutOrder { Provider = "Revolut", Channel = "card" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(started + 1, Metrics.PaymentsStarted);
        Assert.Equal(outages, Metrics.GatewayOutages);
    }

    [Fact]
    public async Task When_The_Gateway_Is_Down_Should_Count_The_Outage_And_Still_Answer_503_With_A_Safe_Message()
    {
        var order = await CreateOrderAsync("outage-counted@demo.example");
        var (started, outages) = (Metrics.PaymentsStarted, Metrics.GatewayOutages);

        app.Gateways.Unavailable = true;
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync($"/api/orders/{order}/checkout", new CheckoutOrder { Provider = "Revolut", Channel = "card" });
        }
        finally
        {
            app.Gateways.Unavailable = false;
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("The payment gateway is not available. Try again in a moment.", error?.Message);   // nothing of the transport error
        Assert.Equal(outages + 1, Metrics.GatewayOutages);   // the exception action ran, and the exception went on
        Assert.Equal(started, Metrics.PaymentsStarted);      // the post-processor did not
    }

    // ─── A request without a response, and an exception handler ─────────────────────────────────────────

    [Fact]
    public async Task When_Cancelling_An_Order_Should_Answer_204_And_Mark_It_Cancelled()
    {
        var order = await CreateOrderAsync("cancel@demo.example");

        var response = await client.DeleteAsync($"/api/orders/{order}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(nameof(OrderStatus.Cancelled), (await client.GetOrderAsync(order))?.Status);
    }

    [Fact]
    public async Task When_Cancelling_An_Order_That_Does_Not_Exist_Should_Answer_204_Because_The_Exception_Is_Handled()
    {
        var response = await client.DeleteAsync("/api/orders/ORD-NOPE");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ─── A notification with two handlers ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task When_An_Order_Is_Paid_Should_Run_Both_Notification_Handlers_And_Refuse_To_Cancel_It()
    {
        var order = await CreateOrderAsync("both-handlers@demo.example");
        var checkout = await client.PostAsJsonAsync($"/api/orders/{order}/checkout", new CheckoutOrder { Provider = "Revolut", Channel = "card" });
        var paymentId = (await checkout.Content.ReadFromJsonAsync<CheckoutOrderResponse>())!.PaymentId;
        var paidBefore = Metrics.OrdersPaid;

        var response = await client.SendAsync(SignedNotification($$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(app.Outbox.Messages, message => message.Recipient == "both-handlers@demo.example");   // SendOrderConfirmation
        Assert.Equal(paidBefore + 1, Metrics.OrdersPaid);                                                    // RecordOrderPaid

        var cancel = await client.DeleteAsync($"/api/orders/{order}");
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal(nameof(OrderStatus.Paid), (await client.GetOrderAsync(order))?.Status);
    }

    [Fact]
    public async Task When_The_Mail_Cannot_Be_Sent_Should_Still_Accept_The_Payment_And_Run_The_Other_Handler()
    {
        using var broken = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddTransient<IEmailProvider, UnreachableMailServer>()));
        var brokenClient = broken.CreateClient();
        var created = await brokenClient.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = "no-mail@demo.example", ProductIds = [2, 4] });
        var order = (await created.Content.ReadOrderAsync())!.Number;
        var checkout = await brokenClient.PostAsJsonAsync($"/api/orders/{order}/checkout", new CheckoutOrder { Provider = "Revolut", Channel = "card" });
        var paymentId = (await checkout.Content.ReadFromJsonAsync<CheckoutOrderResponse>())!.PaymentId;
        var metrics = broken.Services.GetRequiredService<ShopMetrics>();
        var paidBefore = metrics.OrdersPaid;

        var response = await brokenClient.SendAsync(SignedNotification($$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}"""));

        // The gateway is told the notification arrived, the order is paid, and the sale is counted.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(OrderStatus.Paid), (await brokenClient.GetOrderAsync(order))?.Status);
        Assert.Equal(paidBefore + 1, metrics.OrdersPaid);
    }

    // ─── Configuration ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void When_There_Is_No_Signing_Key_Should_Refuse_To_Start()
    {
        // The key lives in appsettings.Development.json; another environment has to bring its own.
        using var production = app.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.Contains("Jwt:Key", exception.GetBaseException().Message);
    }

    private async Task<string> CreateOrderAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrder { CustomerEmail = email, ProductIds = [2, 4] });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadOrderAsync())!.Number;
    }

    /// <summary>A notification signed the way Revolut signs it: HMAC-SHA256 of <c>v1.{timestamp}.{body}</c>.</summary>
    private HttpRequestMessage SignedNotification(string body)
    {
        var secret = app.Services.GetRequiredService<IConfiguration>()["Payments:Providers:Revolut:WebhookSecret"]!;
        var timestamp = DemoApplication.Now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"v1.{timestamp}.{body}")));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/Revolut")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Revolut-Request-Timestamp", timestamp);
        request.Headers.Add("Revolut-Signature", $"v1={signature}");
        return request;
    }

    private sealed class UnreachableMailServer : IEmailProvider
    {
        public Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]>? attachments)
            => throw new InvalidOperationException("The mail server did not answer.");

        public Task<ICollection<MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
            => Task.FromResult<ICollection<MailMessage>>([]);
    }
}
