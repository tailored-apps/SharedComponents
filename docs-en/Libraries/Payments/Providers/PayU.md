# TailoredApps.Shared.Payments.Provider.PayU

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.PayU)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.PayU/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with the **PayU REST API v2.1** — a leading Polish payment operator that handles BLIK, fast transfers, cards and other methods.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayU
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.PayU;

builder.Services
    .AddPayments()
    .RegisterPayUProvider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "PayU": {
        "ClientId": "your-client-id",
        "ClientSecret": "your-client-secret",
        "PosId": "your-pos-id",
        "SignatureKey": "your-signature-key",
        "ServiceUrl": "https://secure.snd.payu.com",
        "NotifyUrl": "https://myapp.com/webhooks/payu",
        "ContinueUrl": "https://myapp.com/payment/return"
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `ClientId` | yes | OAuth client identifier |
| `ClientSecret` | yes | OAuth client secret |
| `PosId` | yes | Point of sale identifier (merchantPosId) |
| `SignatureKey` | yes | Key for verifying notification signatures (second key) |
| `ServiceUrl` | no | PayU API URL. The default is the sandbox, `https://secure.snd.payu.com` - set the production address for real payments |
| `NotifyUrl` | no | URL for transaction notifications (webhook) |
| `ContinueUrl` | no | Return URL after payment |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

The list is fixed in the provider:

| Currency | Channels |
|--------|--------|
| PLN | `blik` (BLIK), `c` (payment card), `o` (online transfer), `ai` (instalments), `ap` (Apple Pay), `jp` (Google Pay) |
| Other | `c` |

---

## Authorization

The provider obtains an OAuth2 token (`client_credentials`) for every `RegisterPayment` and `GetStatus` call. The token is not cached.

---

## Webhook

PayU sends POST notifications with the `OpenPayU-Signature` header (`signature=...;algorithm=...`). The provider hashes the raw body
followed by `SignatureKey` with the algorithm the header names - `MD5`, `SHA-1` or `SHA-256` (`MD5` when it names none) - and
compares the result with `signature`.

```csharp
[HttpPost("webhooks/payu")]
public async Task<IActionResult> PayUWebhook(CancellationToken cancellationToken)
{
    // The signature covers the exact bytes of the body - read it raw, not through [FromBody].
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("PayU", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

| `order.status` | Result |
|----------------|--------|
| `COMPLETED` | `PaymentWebhookProcessed`, status `Finished` |
| `CANCELED`, `REJECTED` | `PaymentWebhookProcessed`, status `Rejected` |
| anything else / absent | `PaymentWebhookIgnored` (status `Processing`) |

---

## 🔒 Security

- Orders are created through the named HTTP client `PayU.Orders` (`PayUServiceCaller.OrdersClientName`), the other calls through `PayU`. `RegisterPayUProvider()` configures `PayU.Orders` not to follow redirects and without cookies: PayU answers an order with a 302 and the order id is in the body of that answer. If you configure the client yourself, keep `AllowAutoRedirect = false` - otherwise the provider throws `InvalidOperationException` rather than lose the id.
- The default `ServiceUrl` is the PayU sandbox (`https://secure.snd.payu.com`) - set the production address explicitly.
- Every order is sent with `customerIp` = `127.0.0.1`; the payer's address is not forwarded to PayU.
- Empty `SignatureKey` or no `signature=` in the `OpenPayU-Signature` header → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed; constant-time comparison.
- Supported algorithms: `MD5`, `SHA-1`, `SHA-256` (matching the POS setting in the PayU panel); other `algorithm=` values are rejected. SHA-256 is recommended.
- The webhook result carries `PaymentUniqueId` (`order.orderId`).

---

## 🤖 AI Agent Prompt

```markdown
## PayU Provider - instructions for an AI agent

Provider key: "PayU"

Configuration section: "Payments:Providers:PayU"

Required fields: ClientId, ClientSecret, PosId, SignatureKey

Authorization: OAuth2 client_credentials — automatic, a token is requested for every call (not cached)

Amount in the API: grosze (int) — the library converts automatically from decimal

Sandbox: ServiceUrl = "https://secure.snd.payu.com" (this is the default - set the production address explicitly)

Registration: builder.Services.AddPayments().RegisterPayUProvider();
- Configure SHA-256 for notification signatures in the PayU panel
- Webhook: pass the raw body (not [FromBody]) and every request header; answer a PaymentWebhookRejected result with a non-2xx status
```
