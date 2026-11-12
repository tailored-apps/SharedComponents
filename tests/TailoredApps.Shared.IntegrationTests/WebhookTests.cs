using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Demo.Data;
using TailoredApps.Demo.Orders;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// A gateway notification, end to end: the Revolut provider checks the signature, the demo marks the order
/// paid inside the unit of work and sends the confirmation through MediatR.Email and the Email template builder.
/// </summary>
public class WebhookTests(DemoApplication app) : IClassFixture<DemoApplication>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task When_A_Signed_Completion_Arrives_Should_Mark_The_Order_Paid_And_Send_The_Confirmation()
    {
        var (order, paymentId) = await CheckoutAsync("paid@demo.example");

        var response = await client.SendAsync(SignedNotification($$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}"""));

        var answer = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {answer}");
        Assert.Equal("OK", answer);

        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.Paid), stored?.Status);
        Assert.Equal(DemoApplication.Now, stored?.PaidAt);

        var mail = Assert.Single(app.Outbox.Messages, m => m.Recipient == "paid@demo.example");
        Assert.Equal($"Order {order.Number} is paid", mail.Topic);
        Assert.Contains($"<strong>{order.Number}</strong>", mail.Body);   // the template, with the variables filled in
        Assert.Contains("Amount: 73.50 PLN", mail.Body);
    }

    [Fact]
    public async Task When_The_Same_Completion_Arrives_Twice_Should_Send_One_Confirmation()
    {
        var (_, paymentId) = await CheckoutAsync("twice@demo.example");
        var body = $$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}""";

        await client.SendAsync(SignedNotification(body));
        var again = await client.SendAsync(SignedNotification(body));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(app.Outbox.Messages, m => m.Recipient == "twice@demo.example");
    }

    [Fact]
    public async Task When_A_Declined_Payment_Arrives_Should_Mark_The_Order_Rejected()
    {
        var (order, paymentId) = await CheckoutAsync("declined@demo.example");

        var response = await client.SendAsync(SignedNotification($$"""{"event":"ORDER_PAYMENT_DECLINED","order_id":"{{paymentId}}"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.PaymentRejected), stored?.Status);
        Assert.DoesNotContain(app.Outbox.Messages, m => m.Recipient == "declined@demo.example");
    }

    [Fact]
    public async Task When_The_Signature_Does_Not_Match_Should_Answer_400_And_Change_Nothing()
    {
        var (order, paymentId) = await CheckoutAsync("forged@demo.example");
        var request = SignedNotification($$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}""", secret: "a-guessed-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidSignature", await response.Content.ReadAsStringAsync());
        var stored = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Number}");
        Assert.Equal(nameof(OrderStatus.AwaitingPayment), stored?.Status);
    }

    [Fact]
    public async Task When_The_Signature_Is_Older_Than_The_Tolerance_Should_Answer_400()
    {
        var (_, paymentId) = await CheckoutAsync("replayed@demo.example");
        var request = SignedNotification($$"""{"event":"ORDER_COMPLETED","order_id":"{{paymentId}}"}""", sentAt: DemoApplication.Now.AddMinutes(-10));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(OrderDto Order, string PaymentId)> CheckoutAsync(string email)
    {
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrder(email, [2, 4]));   // 19.50 + 54.00
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;

        var checkout = await client.PostAsJsonAsync($"/api/orders/{order.Number}/checkout", new Checkout("Revolut", "card"));
        checkout.EnsureSuccessStatusCode();
        var result = (await checkout.Content.ReadFromJsonAsync<CheckoutResult>())!;
        return (order, result.PaymentId!);
    }

    /// <summary>A notification signed the way Revolut signs it: HMAC-SHA256 of <c>v1.{timestamp}.{body}</c>.</summary>
    private HttpRequestMessage SignedNotification(string body, string? secret = null, DateTimeOffset? sentAt = null)
    {
        secret ??= app.Services.GetRequiredService<IConfiguration>()["Payments:Providers:Revolut:WebhookSecret"]!;
        var timestamp = (sentAt ?? DemoApplication.Now).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"v1.{timestamp}.{body}")));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/Revolut")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Revolut-Request-Timestamp", timestamp);
        request.Headers.Add("Revolut-Signature", $"v1={signature}");
        return request;
    }
}
