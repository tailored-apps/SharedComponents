# TailoredApps.Shared.Payments.Provider.PayNow

PayNow is the payment gateway of mBank (BLIK, cards, bank transfers). This package plugs the PayNow REST API (`/v2/payments`)
into `TailoredApps.Shared.Payments`: it starts payments, reads their status, and verifies and handles PayNow notifications.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayNow   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.PayNow;

builder.Services.AddPayments().RegisterPayNowProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterPayNowProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:PayNow` section (`PayNowServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "PayNow": {
        "ApiKey": "<paynow-api-key>",
        "SignatureKey": "<paynow-signature-key>",
        "ServiceUrl": "https://api.paynow.pl",
        "ReturnUrl": "https://shop.example/payment/return",
        "ContinueUrl": "https://shop.example/payment/continue"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ApiKey` | yes | Sent with every call in the `Api-Key` header | - |
| `SignatureKey` | yes | Key of the HMAC that verifies notifications | - |
| `ServiceUrl` | no | Base URL of the PayNow API (`ApiUrl` is an obsolete alias of this key) | `https://api.paynow.pl` |
| `ReturnUrl` | no | Sent as `returnUrl` of the payment | empty |
| `ContinueUrl` | no | Sent as `continueUrl` of the payment | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("PayNow", 49.99m, "PLN")
{
    Title = "Order 7",
    Email = "buyer@example.com",
    AdditionalData = "order-7",     // sent as externalId and as the Idempotency-Key header
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

`response.PaymentUniqueId` is the PayNow `paymentId`; pass it to `GetStatus("PayNow", paymentId)`.

## Webhooks

PayNow posts a JSON body and signs it in the `Signature` header: Base64 of HMAC-SHA256 over the raw body, keyed with
`SignatureKey`. The provider recomputes it and compares in constant time, so the body must reach it byte for byte.

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var result = await payments.HandleWebhookAsync("PayNow", new PaymentWebhookRequest
{
    Body = await reader.ReadToEndAsync(cancellationToken),   // raw body, not [FromBody]
    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
}, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

| `status` in the body | Result |
| --- | --- |
| `CONFIRMED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| `ERROR`, `REJECTED`, `ABANDONED`, `EXPIRED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| anything else | `PaymentWebhookIgnored` |
| signature missing or wrong, or `SignatureKey` not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

`Payment.PaymentUniqueId` is the `paymentId` of the notification - the same identifier `RegisterPayment` returned.

## Good to know

- A failing gateway is an exception, not a rejected payment: `RegisterPayment` throws `PaymentGatewayException` (derives from
  `HttpRequestException`) on 5xx, 408 or 429, and `GetStatus` throws it on any answer outside 2xx. `Rejected` from
  `RegisterPayment` means PayNow answered without a `paymentId`.
- Set `AdditionalData` to a stable order reference. It is the idempotency key; without it the provider uses a random value and
  a retried request creates a second payment.
- `GetStatus` maps `CONFIRMED` to `Finished`, `PENDING` and `PROCESSING` to `Processing`, `ERROR`, `REJECTED`, `ABANDONED` and
  `EXPIRED` to `Rejected`, and `NEW` or an unknown status to `Created`.
- The amount is sent in the minor unit (amount x 100). `GetChannels` returns a fixed list (`BLIK`, `CARD`, `PBL`, `TRANSFER`);
  `PaymentRequest.PaymentChannel` is not sent to PayNow.
- Requests go through the named HTTP client `PayNow` from `IHttpClientFactory`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/PayNow/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
