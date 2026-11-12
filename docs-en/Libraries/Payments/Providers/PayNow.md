# TailoredApps.Shared.Payments.Provider.PayNow

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.PayNow)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.PayNow/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **PayNow** — mBank's payment gateway that handles BLIK, fast transfers and payment cards.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayNow
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.PayNow;

builder.Services
    .AddPayments()
    .RegisterPayNowProvider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "PayNow": {
        "ApiKey": "my-api-key",
        "SignatureKey": "my-signature-key",
        "ServiceUrl": "https://api.paynow.pl",
        "ReturnUrl": "https://myapp.com/payment/return",
        "ContinueUrl": "https://myapp.com/payment/continue"
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `ApiKey` | yes | API key for authorizing requests (`Api-Key` header) |
| `SignatureKey` | yes | Key for verifying webhook signatures |
| `ServiceUrl` | no | PayNow API URL, default `https://api.paynow.pl` (sandbox: `https://api.sandbox.paynow.pl`). The old alias `ApiUrl` was removed in 3.0: a configuration that still sets it stops the host from starting instead of silently going to production |
| `ReturnUrl` | no | Return URL after payment |
| `ContinueUrl` | no | Continuation URL after returning from the PayNow page |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

The provider returns a fixed list for every currency: `BLIK`, `CARD` (payment card), `PBL` (pay-by-link bank transfer), `TRANSFER` (traditional bank transfer).

---

## Webhook

PayNow sends POST notifications. The signature is verified with SHA-256 HMAC using `SignatureKey` and is passed in the `Signature` header.

---

## 🔒 Security

- Empty `SignatureKey` or an empty signature → rejected (fail-closed); constant-time comparison.
- The `Idempotency-Key` header is stable for the same `AdditionalData` (externalId), so a retried request does not create a second payment.
- `EXPIRED` maps to `Rejected`; the webhook result carries `PaymentUniqueId` (`paymentId`).

---

## 🤖 AI Agent Prompt

```markdown
## PayNow Provider - instructions for an AI agent

Provider key: "PayNow"

Configuration section: "Payments:Providers:PayNow"

Required fields: ApiKey, SignatureKey

Amount in the API: grosze (int), e.g. 1999 = 19.99 PLN — the library converts automatically

Sandbox URL: https://api.sandbox.paynow.pl

Registration: builder.Services.AddPayments().RegisterPayNowProvider();
- Pass a stable order identifier in AdditionalData - it doubles as the Idempotency-Key and externalId
```
