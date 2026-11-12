# TailoredApps.Shared.Payments.Provider.Revolut

Revolut is a payment operator (cards, Revolut Pay). This package plugs the Revolut Merchant API into
`TailoredApps.Shared.Payments`: it creates orders and returns the address of the checkout page, reads the state of an order,
and verifies and handles Revolut webhooks.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Revolut   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Revolut;

builder.Services.AddPayments().RegisterRevolutProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterRevolutProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:Revolut` section (`RevolutServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "Revolut": {
        "ApiKey": "<merchant-api-key>",
        "WebhookSecret": "<webhook-signing-secret>",
        "ApiUrl": "https://merchant.revolut.com/api",
        "WebhookToleranceSeconds": 300
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ApiKey` | yes | Merchant API key, sent as a `Bearer` token | - |
| `WebhookSecret` | yes | Signing secret Revolut issued when the webhook was registered | - |
| `ApiUrl` | no | Base URL of the Merchant API; orders are at `{ApiUrl}/1.0/orders` | `https://merchant.revolut.com/api` |
| `WebhookToleranceSeconds` | no | Largest accepted age of a webhook timestamp, in either direction; `0` switches the check off | `300` |
| `ReturnUrl` | no | Bound from configuration, but not sent to Revolut by the provider | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("Revolut", 49.99m, "PLN")
{
    Title = "Order 7",
    Email = "buyer@example.com",
    AdditionalData = "order-7",     // sent as merchant_order_ext_ref
}, cancellationToken);

return Redirect(response.RedirectUrl);   // checkout_url of the order
```

`response.PaymentUniqueId` is the Revolut order `id`; pass it to `GetStatus("Revolut", orderId)`.

## Webhooks

Revolut posts a JSON body with two headers: `Revolut-Request-Timestamp` (milliseconds) and `Revolut-Signature` (`v1=<hex>`).
The provider computes HMAC-SHA256 over `v1.{timestamp}.{raw body}` keyed with `WebhookSecret` and compares in constant time.
The header may carry several signatures separated by commas; one match is enough. A timestamp further than
`WebhookToleranceSeconds` from the server clock is refused, which stops a captured webhook from being replayed.

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var result = await payments.HandleWebhookAsync("Revolut", new PaymentWebhookRequest
{
    Body = await reader.ReadToEndAsync(cancellationToken),   // raw body, not [FromBody]
    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
}, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

| `event` in the body | Result |
| --- | --- |
| `ORDER_COMPLETED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| `ORDER_CANCELLED`, `ORDER_PAYMENT_DECLINED`, `PAYMENT_DECLINED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| `ORDER_AUTHORISED` or any other event | `PaymentWebhookIgnored` |
| signature or timestamp missing, wrong or too old, or `WebhookSecret` not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

`Payment.PaymentUniqueId` is the `order_id` of the notification - the same identifier `RegisterPayment` returned.

## Good to know

- A failing gateway is an exception, not a rejected payment: `RegisterPayment` throws `PaymentGatewayException` (derives from
  `HttpRequestException`) on 5xx, 408 or 429, and `GetStatus` throws it on any answer outside 2xx. `Rejected` from
  `RegisterPayment` means Revolut answered without an order `id`.
- `GetStatus` maps the order state `completed` to `Finished`, `processing`, `pending` and `authorised` to `Processing`, `failed`
  and `cancelled` to `Rejected`, anything else to `Created`.
- Keep the server clock synchronised: with the default tolerance a webhook is refused when its timestamp differs from the clock
  by more than 300 seconds.
- The amount is sent in the minor unit (amount x 100). `GetChannels` returns a fixed list (`card`, `revolut_pay`);
  `PaymentRequest.PaymentChannel` is not sent to Revolut.
- Requests go through the named HTTP client `Revolut` from `IHttpClientFactory`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/Revolut/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
