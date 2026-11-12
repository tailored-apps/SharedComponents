# TailoredApps.Shared.Payments.Provider.Przelewy24

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Przelewy24)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Przelewy24/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **Przelewy24** — one of the most popular Polish online payment operators, handling more than 170 banks and BLIK.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Przelewy24
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.Przelewy24;

builder.Services
    .AddPayments()
    .RegisterPrzelewy24Provider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Przelewy24": {
        "MerchantId": 12345,
        "PosId": 12345,
        "ApiKey": "your-api-key",
        "CrcKey": "your-crc-key",
        "ServiceUrl": "https://secure.przelewy24.pl",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/przelewy24"
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `MerchantId` | yes | Merchant identifier in Przelewy24 (a number greater than 0) |
| `PosId` | yes | Point of sale identifier (usually = MerchantId; a number greater than 0) |
| `ApiKey` | yes | API key for authorizing requests |
| `CrcKey` | yes | CRC key for transaction signatures and notification verification |
| `ServiceUrl` | no | API URL, default `https://secure.przelewy24.pl` (sandbox: `https://sandbox.przelewy24.pl`) |
| `ReturnUrl` | no | Return URL after payment |
| `NotifyUrl` | no | URL for transaction notifications (webhook) |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

The provider returns a fixed list for every currency: `online_transfer` (online transfer), `blik` (BLIK), `card` (payment card).

---

## Webhook

Przelewy24 posts a JSON notification to `NotifyUrl`. Its signature is the `sign` field of the body (SHA-384 with the CRC key); there is
no signature header, so pass the raw body in `PaymentWebhookRequest.Body`. After checking the signature the provider confirms the
transaction through the `/transaction/verify` endpoint and only then returns the result:

| Outcome | Result |
|---------|--------|
| Przelewy24 accepts the verification | `PaymentWebhookProcessed`, status `Finished` |
| Przelewy24 refuses the verification (4xx) | `PaymentWebhookProcessed`, status `Rejected` |
| the verification call fails (5xx, 408, 429) or the notification cannot be processed | `PaymentWebhookRejected` (`ProcessingFailed`) |
| `sessionId`, `amount`, `currency` or `orderId` missing | `PaymentWebhookRejected` (`MalformedPayload`) |
| `sign` missing or wrong | `PaymentWebhookRejected` (`InvalidSignature`) |

Answer a `PaymentWebhookRejected` result with a status outside 2xx - Przelewy24 then sends the notification again.

---

## 🔒 Security

- The notification sign is SHA-384 over the JSON `{"merchantId","posId","sessionId","amount","originAmount","currency","orderId","methodId","statement","crc"}` in that order (P24 documentation), with unicode and slashes left unescaped. The `verify` call is signed over `{"sessionId","orderId","amount","currency","crc"}`.
- Empty `CrcKey` or a missing `sign` field → rejected (fail-closed); constant-time comparison.
- The webhook result's `PaymentUniqueId` is the `sessionId`.
- `GetStatus` does not call Przelewy24 and always returns `Processing` - the outcome of a payment comes only from the webhook.

---

## 🤖 AI Agent Prompt

```markdown
## Przelewy24 Provider - instructions for an AI agent

Provider key: "Przelewy24"

Configuration section: "Payments:Providers:Przelewy24"

Required fields: MerchantId, PosId, ApiKey, CrcKey

Amount: grosze (int) — the library converts automatically

Sandbox: ServiceUrl = "https://sandbox.przelewy24.pl"

Registration: builder.Services.AddPayments().RegisterPrzelewy24Provider();
- Do not reorder the sign fields - P24 hashes the JSON in exactly the documented order
- GetStatus always returns Processing without calling Przelewy24 - rely on the webhook
```
