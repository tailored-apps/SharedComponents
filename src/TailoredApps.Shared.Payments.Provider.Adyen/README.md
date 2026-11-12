# TailoredApps.Shared.Payments.Provider.Adyen

Adyen is a global payment operator (cards, BLIK, iDEAL and other methods). This package plugs the Adyen Checkout API v71 into
`TailoredApps.Shared.Payments`: it creates a hosted checkout session and returns the address of Adyen's payment page, reads
the outcome of a session, and verifies and handles standard Adyen webhooks.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Adyen   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Adyen;

builder.Services.AddPayments().RegisterAdyenProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterAdyenProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:Adyen` section (`AdyenServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "Adyen": {
        "ApiKey": "<adyen-api-key>",
        "MerchantAccount": "<merchant-account>",
        "NotificationHmacKey": "<webhook-hmac-key-hex>",
        "ReturnUrl": "https://shop.example/payment/return",
        "Environment": "test"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ApiKey` | yes | API key, sent in the `X-API-Key` header | - |
| `MerchantAccount` | yes | Merchant account the sessions are created for | - |
| `NotificationHmacKey` | yes | HMAC key of the webhook, as a hex string | - |
| `ReturnUrl` | no | Address Adyen sends the shopper back to (`returnUrl` of the session) | empty |
| `Environment` | no | `test` calls `https://checkout-test.adyen.com/v71`; any other value calls `https://checkout-live.adyen.com/v71`. `IsTest` (`true` / `false`) is the same switch | `test` |
| `CheckoutUrl` | no | Full base URL of the Checkout API; when set it replaces the address derived from `Environment` | not set |
| `ClientKey` | no | Bound from configuration, but not used by the provider | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("Adyen", 49.99m, "PLN")
{
    Email = "buyer@example.com",
    AdditionalData = "order-7",     // sent as the session reference; comes back as merchantReference in the webhook
}, cancellationToken);

return Redirect(response.RedirectUrl);   // response.PaymentUniqueId is the session id
```

When the shopper comes back, the return address carries the `sessionId` and `sessionResult` query parameters:

```csharp
var payment = await payments.GetStatus("Adyen", AdyenProvider.SessionResultId(sessionId, sessionResult), cancellationToken);
```

## Webhooks

Adyen posts JSON with a `notificationItems` array. There is no signature header: each `NotificationRequestItem` carries
`additionalData.hmacSignature`, the Base64 HMAC-SHA256 (key: `NotificationHmacKey` decoded from hex) of eight fields joined
with a colon - `pspReference`, `originalReference`, `merchantAccountCode`, `merchantReference`, `amount.value`,
`amount.currency`, `eventCode`, `success` (an absent field is an empty string). The first item of the array is handled.

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var body = await reader.ReadToEndAsync(cancellationToken);   // raw body, not [FromBody]
var result = await payments.HandleWebhookAsync("Adyen", new PaymentWebhookRequest { Body = body }, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Content(AdyenProvider.Acknowledgement, "text/plain");
```

| `eventCode` | `success` | Result |
| --- | --- | --- |
| `AUTHORISATION`, `REFUND` | `true` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| `AUTHORISATION`, `REFUND` | anything else or absent | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| `CANCELLATION`, `AUTHORISATION_FAILED` | any | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| another event | any | `PaymentWebhookIgnored` |

A wrong or missing signature gives `PaymentWebhookRejected` (`InvalidSignature`); a body that is not JSON or has no
`NotificationRequestItem` gives `PaymentWebhookRejected` (`MalformedPayload`).

## Good to know

- Answer an accepted notification, an ignored one included, with HTTP 200 and the body `[accepted]`
  (`AdyenProvider.Acknowledgement`).
- `GetStatus` needs the id built by `AdyenProvider.SessionResultId(sessionId, sessionResult)`; a bare session id throws
  `ArgumentException`. It maps `completed` to `Finished`, `paymentPending` to `Processing`, `refused`, `canceled` and `expired`
  to `Rejected`, anything else to `Created`.
- `Payment.PaymentUniqueId` of a webhook result is `merchantReference` (your `AdditionalData`), or `pspReference` when it is
  empty - not the session id. Without `AdditionalData` the provider sends a random reference, so always set it.
- The signature covers only the eight fields above. Do not base decisions on other fields of the notification.
- A failing gateway is an exception, not a rejected payment: `RegisterPayment` throws `PaymentGatewayException` (derives from
  `HttpRequestException`) on 5xx, 408 or 429, and `GetStatus` throws it on any answer outside 2xx. Another refusal of a session
  returns `Rejected` with Adyen's answer in `ResponseObject`. Requests go through the named HTTP client `Adyen`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/Adyen/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
