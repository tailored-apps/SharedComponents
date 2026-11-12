# TailoredApps.Shared.Payments.Provider.CashBill

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.CashBill)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.CashBill/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **CashBill** — a Polish online payment operator that handles bank transfers, cards and BLIK.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.CashBill;

builder.Services
    .AddPayments()
    .RegisterCashbillProvider();
```

---

## Configuration `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Cashbill": {
        "ShopId": "MY_SHOP_ID",
        "ShopSecretPhrase": "my_secret_phrase",
        "ServiceUrl": "https://pay.cashbill.eu",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NegativeReturnUrl": "https://myapp.com/payment/failed"
      }
    }
  }
}
```

| Option | Required | Description |
|-------|------|------|
| `ShopId` | yes | Shop identifier in CashBill |
| `ShopSecretPhrase` | yes | Secret phrase that signs requests and verifies notifications |
| `ServiceUrl` | yes | CashBill REST API URL; there is no default. When the address has a path, end it with `/` - request addresses are resolved relative to it |
| `ReturnUrl` | no | Return URL after a successful payment. Not validated at startup, but `RegisterPayment` fails unless it is an absolute URL |
| `NegativeReturnUrl` | no | Return URL after a failed/cancelled payment |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated.

---

## Supported channels

CashBill returns a dynamic list of channels fetched from the API for the given currency (PLN, EUR, USD etc.). The channels include bank transfer, cards, BLIK, e-wallets.

---

## Webhook

CashBill sends a GET/POST notification with three query-string parameters: `cmd`, `args` (the payment id) and `sign`.
The provider has no option for the notification address. It reads the parameters from `PaymentWebhookRequest.Query`
(not from the body or the headers), checks that `sign` equals `MD5(cmd + args + ShopSecretPhrase)` and then reads the current
status of the payment from the CashBill API.

```csharp
[AcceptVerbs("GET", "POST", Route = "webhooks/cashbill")]
public async Task<IActionResult> CashBillWebhook(CancellationToken cancellationToken)
{
    var result = await _payments.HandleWebhookAsync("Cashbill", new PaymentWebhookRequest
    {
        HttpMethod = Request.Method,
        Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

| Notification | Result |
|--------------|--------|
| valid signature | `PaymentWebhookProcessed` with the status read from CashBill (`Created`, `Processing`, `Finished` or `Rejected`) |
| `args` missing | `PaymentWebhookRejected` (`MalformedPayload`) |
| `sign` missing or wrong | `PaymentWebhookRejected` (`InvalidSignature`) |
| `ShopSecretPhrase` not configured | `PaymentWebhookRejected` (`NotConfigured`) |

---

## 🔒 Security

- API calls go through `HttpClient` - the named client `CashBill` (`CashbillHttpClient.HttpClientName`) from `IHttpClientFactory`, so the handlers, resilience and timeouts configured in the host apply. An answer outside 2xx throws `PaymentGatewayException` (up to 2.0.1: `WebException`) - catch `HttpRequestException`. Failures are logged with the address but without the query string, which carries the request signature.
- The notification signature is `MD5(cmd + args + ShopSecretPhrase)`, compared in constant time. Both `HandleWebhookAsync` and the legacy `TransactionStatusChange` **verify** it - an invalid signature yields `PaymentWebhookRejected` (`InvalidSignature`, message "Invalid signature.") / a `Rejected` response without polling the CashBill API.
- The error message no longer contains the expected signature (it used to be an oracle for forging arbitrary notifications).
- Missing `ShopSecretPhrase` → `PaymentWebhookRejected` (`NotConfigured`, message "Signature verification is not configured.").
- `GetStatus(paymentId)` rejects identifiers with special characters (`PaymentIdentifier.EnsureSafe`).
- A webhook with a valid signature always gives `PaymentWebhookProcessed` with the status polled from CashBill - never `PaymentWebhookIgnored`. Check `Payment.PaymentStatus` before acting: it can still be `Created` or `Processing`.
- After creating a payment the provider rewrites its return address to `{ReturnUrl}/{paymentId}`: the payer comes back to `ReturnUrl` with the payment id appended.

---

## 🤖 AI Agent Prompt

```markdown
## CashBill Provider - instructions for an AI agent

Provider key: "Cashbill"

Configuration section: "Payments:Providers:Cashbill" (lowercase "b" in Cashbill!)

Required fields: ShopId, ShopSecretPhrase, ServiceUrl

Registration: builder.Services.AddPayments().RegisterCashbillProvider();
- The notification is read from the query string (cmd, args, sign): fill PaymentWebhookRequest.Query
- A webhook result is always PaymentWebhookProcessed with the polled status - check Payment.PaymentStatus
- Never return the webhook ErrorMessage to the caller; log it server-side
- Keep the shop secret in user-secrets/environment variables - never in the repository
```
