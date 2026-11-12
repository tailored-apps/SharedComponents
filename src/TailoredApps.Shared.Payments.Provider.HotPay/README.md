# TailoredApps.Shared.Payments.Provider.HotPay

HotPay is a payment operator (BLIK, cards, bank transfers). This package plugs it into `TailoredApps.Shared.Payments`:
it starts a payment and verifies the notification HotPay sends about its outcome. It does not query HotPay for the
status of a payment.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.HotPay   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.HotPay;

builder.Services.AddPayments().RegisterHotPayProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterHotPayProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:HotPay` section (`HotPayServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "HotPay": {
        "SecretHash": "<hotpay-secret>",
        "ServiceUrl": "https://platnosci.hotpay.pl",
        "ReturnUrl": "https://shop.example/payment/return"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `SecretHash` | yes | The HotPay secret: sent as `SEKRET`, part of the request hash, and the key that verifies notifications | - |
| `ServiceUrl` | no | Address the payment request is posted to | `https://platnosci.hotpay.pl` |
| `ReturnUrl` | no | Sent as `ADRES_WWW`; part of the request hash | empty |
| `NotifyUrl` | no | Bound from configuration, but not sent to HotPay by the provider | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("HotPay", 49.99m, "PLN")
{
    Title = "Order 7",              // sent as NAZWA_USLUGI
    Email = "buyer@example.com",
}, cancellationToken);

if (response.PaymentStatus == PaymentStatusEnum.Rejected || response.RedirectUrl is null)
    return Problem("HotPay did not start the payment.");

// keep response.PaymentUniqueId with the order, then:
return Redirect(response.RedirectUrl);
```

## Webhooks

The provider reads four parameters from `PaymentWebhookRequest.Query`: `HASH`, `KWOTA`, `ID_PLATNOSCI` and `STATUS`. It does
not read the body or the headers. A notification is accepted when `HASH` equals the hex SHA-256 of
`SecretHash;KWOTA;ID_PLATNOSCI;STATUS` (joined with semicolons; compared in constant time, letter case ignored).

```csharp
var result = await payments.HandleWebhookAsync("HotPay", new PaymentWebhookRequest
{
    Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
}, cancellationToken);

return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

If your endpoint receives these values as form fields instead of query-string parameters, copy them into `Query`.

| Notification | Result |
| --- | --- |
| `STATUS` is `SUCCESS` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| any other `STATUS` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| `HASH` missing or wrong, or `SecretHash` not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

`Payment.PaymentUniqueId` is `ID_PLATNOSCI`. This provider never returns `PaymentWebhookIgnored`.

## Good to know

- `GetStatus` makes no call to HotPay and always returns `Processing`. The outcome of a payment comes only from the notification.
- When HotPay answers the payment request with 5xx, 408 or 429, `RegisterPayment` throws `PaymentGatewayException` (derives from
  `HttpRequestException`): the state of the payment is not known. Any other answer outside 2xx is a refusal and returns
  `Rejected` without a `RedirectUrl`. `RedirectUrl` is the `PRZEKIERUJ_DO` field of a successful answer.
- `PaymentUniqueId` is `ID_PLATNOSCI` from HotPay's answer, or - when the answer has none - the identifier the provider generated
  and sent as `IDENTYFIKATOR_PLATNOSCI`. `PaymentRequest.AdditionalData` is not sent to HotPay.
- `GetChannels` returns a fixed list (`blik`, `card`, `transfer`); `PaymentRequest.PaymentChannel` is not sent to HotPay.
- Requests go through the named HTTP client `HotPay` from `IHttpClientFactory`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/HotPay/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
