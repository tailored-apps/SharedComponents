# TailoredApps.Shared.Payments.Provider.CashBill

CashBill is a Polish operator of one-time payments. This package plugs the CashBill REST API into
`TailoredApps.Shared.Payments`: it lists the payment channels of a shop, starts payments, reads their status, and verifies
and handles CashBill status notifications.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.CashBill;

builder.Services.AddPayments().RegisterCashbillProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterCashbillProvider().RegisterTpayProvider()`. Mind the spelling: the extension method, the options
class (`CashbillServiceOptions`) and the provider key use `Cashbill`; the provider class is `CashBillProvider`.

## Configuration

Options are bound from the `Payments:Providers:Cashbill` section (`CashbillServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "Cashbill": {
        "ServiceUrl": "https://<cashbill-rest-api-address>/",
        "ShopId": "<shop-id>",
        "ShopSecretPhrase": "<shop-secret-phrase>",
        "ReturnUrl": "https://shop.example/payment/return",
        "NegativeReturnUrl": "https://shop.example/payment/failed"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ServiceUrl` | yes | Base URL of the CashBill REST API; end it with `/`, request addresses are resolved relative to it | - |
| `ShopId` | yes | Shop identifier | - |
| `ShopSecretPhrase` | yes | Secret that signs the requests and verifies notifications | - |
| `ReturnUrl` | no | Return address after a payment. Not validated at startup, but `RegisterPayment` fails unless it is an absolute URL | empty |
| `NegativeReturnUrl` | no | Return address after a failed or cancelled payment | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var channels = await payments.GetChannels("Cashbill", "PLN", cancellationToken);   // read from the CashBill API

var response = await payments.RegisterPayment(new PaymentRequest("Cashbill", 49.99m, "PLN")
{
    Title = "Order 7",
    Description = "Order 7 at shop.example",
    Email = "buyer@example.com",
    PaymentChannel = channels.First().Id,
    AdditionalData = "order-7",
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

`response.PaymentUniqueId` is the CashBill payment id; pass it to `GetStatus("Cashbill", paymentId)`.

## Webhooks

CashBill calls the notification address (GET or POST) with three query-string parameters: `cmd`, `args` (the payment id) and
`sign`. The provider reads them from `PaymentWebhookRequest.Query`; it does not read the body or the headers. `sign` must equal
the hex MD5 of `cmd + args + ShopSecretPhrase` (compared in constant time). For an authentic notification the provider then
reads the current status of the payment from the CashBill API.

```csharp
var result = await payments.HandleWebhookAsync("Cashbill", new PaymentWebhookRequest
{
    HttpMethod = Request.Method,
    Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
}, cancellationToken);

return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

| Outcome | Result |
| --- | --- |
| signature valid | `PaymentWebhookProcessed` with the current status of the payment (`Created`, `Processing`, `Finished` or `Rejected`) |
| `args` missing | `PaymentWebhookRejected` (`MalformedPayload`) |
| `sign` missing or wrong | `PaymentWebhookRejected` (`InvalidSignature`) |
| `ShopSecretPhrase` not configured | `PaymentWebhookRejected` (`NotConfigured`) |

This provider never returns `PaymentWebhookIgnored`: check `Payment.PaymentStatus` before acting on a processed result.

## Good to know

- Every CashBill answer outside 2xx throws `PaymentGatewayException` (derives from `HttpRequestException`) - from `GetChannels`,
  `RegisterPayment`, `GetStatus` and also from `HandleWebhookAsync`, which queries the status. The state of the payment is then
  not known. Failures are logged without the query string, which carries the request signature.
- Requests go through the named HTTP client `CashBill` (`CashbillHttpClient.HttpClientName`) from `IHttpClientFactory`, so the
  handlers and timeouts the host configures for it apply.
- After creating a payment the provider sets its return address to `{ReturnUrl}/{paymentId}`: the payer comes back to
  `ReturnUrl` with the payment id appended as a path segment.
- Status mapping: `PreStart` and `Start` are `Created`; `PositiveAuthorization` and `NegativeAuthorization` are `Processing`;
  `PositiveFinish` is `Finished`; `NegativeFinish`, `CriticalError`, `Abort`, `Fraud` and `TimeExceeded` are `Rejected`.
- The provider key is `"Cashbill"` and it is compared case-sensitively: `"CashBill"` does not find the provider.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/CashBill/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
