# TailoredApps.Shared.Payments.Provider.Revolut

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Revolut)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Revolut/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with the **Revolut Merchant API** — card and Revolut Pay payments with signature-based webhook verification.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Revolut
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.Revolut;

builder.Services
    .AddPayments()
    .RegisterRevolutProvider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Revolut": {
        "ApiKey": "your-merchant-api-key",
        "ApiUrl": "https://merchant.revolut.com/api",
        "WebhookSecret": "your-webhook-signing-secret",
        "WebhookToleranceSeconds": 300
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `ApiKey` | yes | Revolut Merchant API key (sent as a `Bearer` token) |
| `WebhookSecret` | yes | Secret for verifying the webhook signature |
| `ApiUrl` | no | Revolut Merchant API URL (default `https://merchant.revolut.com/api`) |
| `WebhookToleranceSeconds` | no | Largest accepted age of the webhook timestamp in seconds, default `300`; `0` disables the check |
| `ReturnUrl` | no | Bound from configuration, but not sent to Revolut by the provider |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

The provider returns a fixed list for every currency: `card` (payment card), `revolut_pay` (Revolut Pay).

---

## Webhook

Revolut signs notifications in the `Revolut-Signature` HTTP header and sends the signing time in `Revolut-Request-Timestamp`.
Verification uses HMAC-SHA256 with `WebhookSecret` over the raw body, so pass the raw body and every header in `PaymentWebhookRequest`.

| `event` | Result |
|---------|--------|
| `ORDER_COMPLETED` | `PaymentWebhookProcessed`, status `Finished` |
| `ORDER_CANCELLED`, `ORDER_PAYMENT_DECLINED`, `PAYMENT_DECLINED` | `PaymentWebhookProcessed`, status `Rejected` |
| `ORDER_AUTHORISED` or another event | `PaymentWebhookIgnored` (status `Processing`) |

`PaymentUniqueId` in the result is the `order_id` of the notification.

---

## 🔒 Security

- The signed payload is `v1.{Revolut-Request-Timestamp}.{raw body}` (dots, as documented by Revolut; the previous `v1:` format rejected every genuine notification).
- **Replay protection.** The millisecond timestamp must be within `WebhookToleranceSeconds` (default 300 s) of the server clock; `0` disables the check.
- The `Revolut-Signature` header may carry several signatures (`v1=a,v1=b`) during secret rotation - any match is accepted.
- Empty `WebhookSecret`, a missing signature or timestamp → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed.

```json
{ "Payments": { "Providers": { "Revolut": { "WebhookSecret": "...", "WebhookToleranceSeconds": 300 } } } }
```

---

## 🤖 AI Agent Prompt

```markdown
## Revolut Provider - instructions for an AI agent

Provider key: "Revolut"

Configuration section: "Payments:Providers:Revolut"

Required fields: ApiKey, WebhookSecret

Amount: grosze/cents (int) — the library converts automatically

Registration: builder.Services.AddPayments().RegisterRevolutProvider();
- Do not set WebhookToleranceSeconds = 0 in production - it disables replay protection
```
