using System.Net;
using System.Net.Http.Json;
using TailoredApps.Shared.Payments;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// What the application offers: the registered payment providers and their channels, and the two optional
/// features (MediatR.ML, Email.Office365), which answer 501 until they are configured.
/// </summary>
public class ApplicationTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task When_Listing_Providers_Should_Return_Every_Registered_Gateway()
    {
        var providers = await client.GetFromJsonAsync<List<PaymentProvider>>("/api/payments/providers");

        Assert.NotNull(providers);
        Assert.Equal(
            ["Adyen", "Cashbill", "HotPay", "PayNow", "PayU", "Przelewy24", "Revolut", "Stripe", "Tpay"],
            providers.Select(p => p.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task When_Listing_Channels_Of_A_Provider_With_A_Fixed_List_Should_Return_It()
    {
        var channels = await client.GetFromJsonAsync<List<PaymentChannel>>("/api/payments/providers/Revolut/channels");

        Assert.NotNull(channels);
        Assert.Equal(["card", "revolut_pay"], channels.Select(c => c.Id));
    }

    [Fact]
    public async Task When_Listing_Channels_Of_A_Provider_That_Asks_Its_Gateway_Should_Return_The_Gateway_Answer()
    {
        var channels = await client.GetFromJsonAsync<List<PaymentChannel>>("/api/payments/providers/Cashbill/channels?currency=PLN");

        Assert.NotNull(channels);
        Assert.Contains(channels, c => c.Id == "blik");
    }

    [Fact]
    public async Task When_Image_Classification_Has_No_Model_Should_Answer_501()
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "image", "cup.jpg" } };

        var response = await client.PostAsync("/api/images/classify", form);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task When_The_Office365_Mailbox_Is_Not_Configured_Should_Answer_501()
    {
        var response = await client.GetAsync("/api/inbox");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
