# TailoredApps.Shared.Payments.Provider.HotPay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.HotPay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.HotPay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **HotPay** — a Polish micropayment operator for online payments.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.HotPay
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.HotPay;

builder.Services
    .AddPayments()
    .RegisterHotPayProvider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "HotPay": {
        "SecretHash": "my_secret_hash",
        "ServiceUrl": "https://platnosci.hotpay.pl",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/hotpay"
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `SecretHash` | yes | HotPay secret (`SEKRET`): signs the payment request and verifies notifications |
| `ServiceUrl` | no | HotPay API URL (default `https://platnosci.hotpay.pl`) |
| `ReturnUrl` | no | Return URL after payment (`ADRES_WWW`) |
| `NotifyUrl` | no | Bound from configuration, but not sent to HotPay by the provider |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

The provider returns a fixed list for every currency: `blik` (BLIK), `card` (payment card), `transfer` (online transfer).

---

## Webhook

The provider reads four parameters of the notification from the query string (`PaymentWebhookRequest.Query`): `HASH`, `KWOTA`,
`ID_PLATNOSCI` and `STATUS`. It does not read the body or the headers. `HASH` must equal the SHA-256 (hex) of
`SecretHash;KWOTA;ID_PLATNOSCI;STATUS`.

```csharp
[HttpPost("webhooks/hotpay")]
public async Task<IActionResult> HotPayWebhook(CancellationToken cancellationToken)
{
    var result = await _payments.HandleWebhookAsync("HotPay", new PaymentWebhookRequest
    {
        Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

If the values reach your endpoint as form fields instead of query-string parameters, copy them into `Query`.

| `STATUS` | Result |
|----------|--------|
| `SUCCESS` | `PaymentWebhookProcessed`, status `Finished` |
| anything else | `PaymentWebhookProcessed`, status `Rejected` |

`PaymentUniqueId` in the result is `ID_PLATNOSCI`.

---

## 🔒 Security

- Empty `SecretHash` or an empty or wrong `HASH` → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed; constant-time comparison.
- The signed service name (`NAZWA_USLUGI`) is exactly the one sent in the payment-initiation request.
- `GetStatus` does not call HotPay and always returns `Processing` - the outcome of a payment comes only from the webhook.

---

## 🤖 AI Agent Prompt

```markdown
## HotPay Provider - instructions for an AI agent

Provider key: "HotPay"

Configuration section: "Payments:Providers:HotPay"

Required fields: SecretHash

Registration: builder.Services.AddPayments().RegisterHotPayProvider();
- The notification is read from the query string (HASH, KWOTA, ID_PLATNOSCI, STATUS): fill PaymentWebhookRequest.Query
- GetStatus always returns Processing without calling HotPay - rely on the webhook
- Keep SecretHash out of the repository; without it every notification is rejected
```
