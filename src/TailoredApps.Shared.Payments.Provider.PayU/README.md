# TailoredApps.Shared.Payments.Provider.PayU

PayU is a payment operator (bank transfers, BLIK, cards, instalments). This package plugs the PayU REST API v2.1 into
`TailoredApps.Shared.Payments`: it creates orders, reads their status, and verifies and handles PayU notifications.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayU   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.PayU;

builder.Services.AddPayments().RegisterPayUProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterPayUProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:PayU` section (`PayUServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "PayU": {
        "ClientId": "<oauth-client-id>",
        "ClientSecret": "<oauth-client-secret>",
        "PosId": "<pos-id>",
        "SignatureKey": "<second-key>",
        "ServiceUrl": "https://secure.snd.payu.com",
        "NotifyUrl": "https://shop.example/webhooks/payu",
        "ContinueUrl": "https://shop.example/payment/return"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ClientId` | yes | OAuth `client_id` | - |
| `ClientSecret` | yes | OAuth `client_secret` | - |
| `PosId` | yes | Point of sale, sent as `merchantPosId` | - |
| `SignatureKey` | yes | The second key; verifies notification signatures | - |
| `ServiceUrl` | no | Base URL of the PayU API | `https://secure.snd.payu.com` (the sandbox) |
| `NotifyUrl` | no | Sent as `notifyUrl` of the order | empty |
| `ContinueUrl` | no | Sent as `continueUrl` of the order | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("PayU", 49.99m, "PLN")
{
    Title = "Order 7",
    Email = "buyer@example.com",
    FirstName = "Jan",
    Surname = "Kowalski",
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

`response.PaymentUniqueId` is the PayU `orderId`; pass it to `GetStatus("PayU", orderId)`.

## Webhooks

PayU posts a JSON body and signs it in the `OpenPayU-Signature` header, a `;`-separated list that carries `signature=<hex>`
and `algorithm=<name>`. The provider hashes the raw body followed by `SignatureKey` with the named algorithm (`MD5`, `SHA-1`
or `SHA-256`; `MD5` when the header names none) and compares in constant time. Any other algorithm is rejected.

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var result = await payments.HandleWebhookAsync("PayU", new PaymentWebhookRequest
{
    Body = await reader.ReadToEndAsync(cancellationToken),   // raw body, not [FromBody]
    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
}, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

| `order.status` in the body | Result |
| --- | --- |
| `COMPLETED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| `CANCELED`, `REJECTED` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| anything else | `PaymentWebhookIgnored` |
| signature missing or wrong, or `SignatureKey` not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

`Payment.PaymentUniqueId` is `order.orderId` - the same identifier `RegisterPayment` returned.

## Good to know

- The default `ServiceUrl` is the PayU sandbox. Set it to the production address for real payments.
- Orders are created through the named HTTP client `PayU.Orders` (`PayUServiceCaller.OrdersClientName`); the token and status
  calls use `PayU`. PayU answers a new order with a 302 whose body holds the order id, so `RegisterPayUProvider()` configures
  `PayU.Orders` with `AllowAutoRedirect = false` and `UseCookies = false`. If you replace its primary handler, keep redirects
  off: a client that followed the redirect makes `RegisterPayment` throw `InvalidOperationException`.
- A failing gateway is an exception, not a rejected payment: `PaymentGatewayException` (derives from `HttpRequestException`) is
  thrown when an order is answered with 5xx, 408 or 429, and when the token or status call is answered outside 2xx. Another
  refusal of an order returns `Rejected` with PayU's answer in `ResponseObject`.
- An OAuth token (`client_credentials`) is requested for every `RegisterPayment` and `GetStatus` call.
- `GetStatus` maps `COMPLETED` to `Finished`, `WAITING_FOR_CONFIRMATION` and `PENDING` to `Processing`, `CANCELED` and
  `REJECTED` to `Rejected`, anything else to `Created`. The amount is sent in the minor unit (amount x 100), and the order is
  sent with `customerIp` `127.0.0.1`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/PayU/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
