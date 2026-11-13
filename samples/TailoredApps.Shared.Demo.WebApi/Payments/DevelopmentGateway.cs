using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ForkForward.Mediator;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Revolut;

namespace TailoredApps.Demo.WebApi.Payments;

/// <summary>
/// Plays the gateway's side of a payment, in the Development environment only (Startup maps it nowhere
/// else): it builds the "payment completed" notification Revolut would send, signs it the way Revolut does
/// with the configured secret, and hands it to the same handler the real webhook endpoint uses. Nothing is
/// skipped - the provider verifies the signature and the timestamp as for any notification.
/// </summary>
public static class DevelopmentGateway
{
    public static IEndpointRouteBuilder MapDevelopmentGatewayEndpoint(this IEndpointRouteBuilder app)
    {
        // POST /api/dev/gateway/revolut/orders/{number}/complete
        app.MapPost("/api/dev/gateway/revolut/orders/{number}/complete", async (
                string number, ISender sender, IOptions<RevolutServiceOptions> options, TimeProvider clock, CancellationToken cancellationToken) =>
            {
                // Revolut names the payment by its own id; the service also finds an order by its number.
                var body = $$"""{"event":"ORDER_COMPLETED","order_id":"{{number}}"}""";
                var timestamp = clock.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
                var signature = Convert.ToHexStringLower(HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(options.Value.WebhookSecret),
                    Encoding.UTF8.GetBytes($"v1.{timestamp}.{body}")));

                var response = await sender.Send(new ReceivePaymentNotification
                {
                    Provider = "Revolut",
                    Notification = new PaymentWebhookRequest
                    {
                        HttpMethod = "POST",
                        Body = body,
                        ContentType = "application/json",
                        Headers = new Dictionary<string, StringValues>
                        {
                            ["Revolut-Request-Timestamp"] = timestamp,
                            ["Revolut-Signature"] = $"v1={signature}",
                        },
                    },
                }, cancellationToken);

                return response.Accepted ? Results.Ok(response) : Results.BadRequest(response);
            })
            .WithExceptionHandling()
            .WithUnitOfWorkTransaction();

        return app;
    }
}
