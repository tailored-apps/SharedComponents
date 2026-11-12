# TailoredApps.Shared.Payments.Provider.Tpay

Tpay is a payment operator (bank transfers, BLIK, cards). This package plugs the Tpay REST API into
`TailoredApps.Shared.Payments`: it creates transactions and returns the address of the payment page, reads the status of a
transaction, and verifies and handles Tpay notifications (the `md5sum` field and the `X-JWS-Signature` header).

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Tpay   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Tpay;

builder.Services.AddPayments().RegisterTpayProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterTpayProvider().RegisterAdyenProvider()`.

## Configuration

Options are bound from the `Payments:Providers:Tpay` section (`TpayServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "Tpay": {
        "ClientId": "<oauth-client-id>",
        "ClientSecret": "<oauth-client-secret>",
        "SecurityCode": "<notification-security-code>",
        "ServiceUrl": "https://api.tpay.com",
        "ReturnUrl": "https://shop.example/payment/return",
        "NotifyUrl": "https://shop.example/webhooks/tpay",
        "JwsCertificateBaseUrl": "https://secure.tpay.com"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `ClientId` | yes | OAuth `client_id` | - |
| `ClientSecret` | yes | OAuth `client_secret` | - |
| `SecurityCode` | yes | Notification verification code from the Tpay panel; the secret of the `md5sum` field | - |
| `ServiceUrl` | no | Base URL of the Tpay API. The old alias `ApiUrl` was removed in 3.0; a configuration that still sets it stops the host from starting, instead of silently falling back to production | `https://api.tpay.com` |
| `ReturnUrl` | no | Payer return address, sent for both the success and the error case | empty |
| `NotifyUrl` | no | Address Tpay posts notifications to | empty |
| `JwsCertificateBaseUrl` | no, but must be an absolute https address | Origin Tpay serves its signing certificates from | `https://secure.tpay.com` |
| `JwsRootCertificatePem` | no, but must be PEM certificates when set | Content of `tpay-jws-root.pem`; pins trust instead of downloading the file | not set |
| `MerchantId` | no | Bound from configuration, but not used by the provider | empty |

A setting that fails these checks stops the host at startup with `OptionsValidationException`.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("Tpay", 49.99m, "PLN")
{
    Title = "Order 7",
    Email = "buyer@example.com",
    AdditionalData = "order-7",     // comes back as the payment id of the webhook result
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

`response.PaymentUniqueId` is the Tpay transaction id; pass it to `GetStatus("Tpay", transactionId)`.

## Webhooks

Tpay posts an `application/x-www-form-urlencoded` body. The provider accepts it only when both checks pass:

1. `md5sum` equals `md5(id + tr_id + tr_amount + tr_crc + SecurityCode)`.
2. The `X-JWS-Signature` header (`TpayProvider.SignatureHeader`) is an RS256 signature over the exact bytes of the body. Its
   certificate is downloaded from the `x5u` address of the header, which must be an https address on `JwsCertificateBaseUrl`,
   and must chain to the Tpay root (`/x509/tpay-jws-root.pem` on that origin, or `JwsRootCertificatePem`).

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var result = await payments.HandleWebhookAsync("Tpay", new PaymentWebhookRequest
{
    Body = await reader.ReadToEndAsync(cancellationToken),   // raw body, not [FromForm]
    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
}, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Content(TpayProvider.Acknowledgement, "text/plain");
```

| `tr_status` | Result |
| --- | --- |
| `TRUE`, `PAID` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Finished` |
| `CHARGEBACK`, `FALSE` | `PaymentWebhookProcessed`, `Payment.PaymentStatus` = `Rejected` |
| anything else | `PaymentWebhookIgnored` |
| either check fails, or `SecurityCode` is not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

## Good to know

- Answer an accepted notification with HTTP 200 and the body `TRUE` (`TpayProvider.Acknowledgement`): that is what Tpay expects
  for a notification it should not send again.
- The transaction id returned by `RegisterPayment` is not in the notification. `Payment.PaymentUniqueId` of a webhook result is
  `tr_crc` - the `PaymentRequest.AdditionalData` you sent - or `tr_id` when `tr_crc` is empty. Always set `AdditionalData`.
- Sandbox: set `JwsCertificateBaseUrl` to `https://secure.sandbox.tpay.com` together with the sandbox `ServiceUrl`.
- A failing gateway is an exception, not a rejected payment: `PaymentGatewayException` (derives from `HttpRequestException`) is
  thrown when creating a transaction is answered with 5xx, 408 or 429, and when the token or status call is answered outside
  2xx. `GetStatus` maps `correct` to `Finished`, `pending` to `Processing`, `error` and `chargeback` to `Rejected`, anything
  else to `Created`.
- Requests and certificate downloads use the named HTTP client `Tpay`. Certificates are cached for one hour and fetched again
  once when a signature does not verify against a cached one. An OAuth token is requested for every `RegisterPayment` and
  `GetStatus` call. `PaymentRequest.PaymentChannel`, when set, is sent as `pay.channel` of the transaction.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/Tpay/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
