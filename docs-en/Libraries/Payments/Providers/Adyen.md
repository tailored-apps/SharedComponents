# TailoredApps.Shared.Payments.Provider.Adyen

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Adyen)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Adyen/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Adyen Checkout API**. Obsługuje płatności kartą, BLIK (PLN) i inne metody dostępne w Adyen, z pełną obsługą webhooków i weryfikacją HMAC.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Adyen
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Adyen;

builder.Services
    .AddPayments()
    .RegisterAdyenProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Adyen": {
        "ApiKey": "AQEyhmf...",
        "MerchantAccount": "MyCompanyECOM",
        "ClientKey": "test_...",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotificationHmacKey": "446a32...hex...",
        "Environment": "test"
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `ApiKey` | Klucz API Adyen (header `X-API-Key`) |
| `MerchantAccount` | Identyfikator konta merchant |
| `ClientKey` | Klucz klienta Drop-in/Components (opcjonalny) |
| `ReturnUrl` | URL powrotu po płatności |
| `NotificationHmacKey` | Klucz HMAC (hex) do weryfikacji webhooków |
| `Environment` | `"test"` lub `"live"` |
| `CheckoutUrl` | Nadpisuje domyślny URL API (opcjonalne) |

---

## Obsługiwane kanały płatności

| Waluta | Kanały |
|--------|--------|
| PLN | `card` (Visa, Mastercard), `blik` |
| EUR | `card`, `sepa` |
| Inne | `card` |

---

## Webhook

A standard Adyen webhook is JSON with a `notificationItems` array; each `NotificationRequestItem` carries its own signature in
`additionalData.hmacSignature` - **there is no signature header**. The signature is HMAC-SHA256 (key: `NotificationHmacKey` decoded from hex,
output in Base64) over eight fields of the item joined with a colon; an absent field is an empty string:

```text
pspReference:originalReference:merchantAccountCode:merchantReference:amount.value:amount.currency:eventCode:success
```

In the Adyen Customer Area (Developers → Webhooks) enable HMAC signing for the webhook and copy the generated key into `NotificationHmacKey`.

```csharp
[HttpPost("webhooks/adyen")]
public async Task<IActionResult> AdyenWebhook(CancellationToken cancellationToken)
{
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("Adyen", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    // Adyen repeats the notification until it gets a 2xx response.
    return result is PaymentWebhookRejected ? Unauthorized() : Content(AdyenProvider.Acknowledgement, "text/plain");
}
```

| `eventCode` | `success` | Result |
|-------------|-----------|--------|
| `AUTHORISATION`, `REFUND` | `true` | `PaymentWebhookProcessed`, status `Finished` |
| `AUTHORISATION`, `REFUND` | anything else / absent | `PaymentWebhookProcessed`, status `Rejected` |
| `CANCELLATION`, `AUTHORISATION_FAILED` | any | `PaymentWebhookProcessed`, status `Rejected` |
| another event | any | `PaymentWebhookIgnored` (status `Processing`) - still acknowledge it |

---

## 🔒 Security

- Verification fails closed: an empty or non-hex `NotificationHmacKey`, a missing `additionalData.hmacSignature` or a signature that does not match →
  `PaymentWebhookRejection.InvalidSignature`; a body that is not JSON or has no `NotificationRequestItem` → `MalformedPayload`. Constant-time comparison.
- The signature covers eight fields, not the whole body: the other fields of the item (for example `paymentMethod`, `reason`) are **not authenticated** - do not base decisions on them.
- A missing `success` field is treated as **failure**.
- The webhook result carries `PaymentUniqueId` (`merchantReference`, falling back to `pspReference`) and `ResponseObject` = `[accepted]`.
- The first item of `notificationItems` is handled (Adyen sends one item per request).
- What was verified: the implementation reproduces the example in Adyen's documentation (the key, the signed text and the expected signature are in the tests);
  send a test notification from the Adyen Customer Area before going live.
- ⚠️ **Known limitation (outside the webhook).** `/sessions` returns `sessionData` for Drop-in/Components, not a redirect address -
  `RequestPayment` therefore returns no `RedirectUrl` for a plain session.

---

## 🤖 AI Agent Prompt

```markdown
## Adyen Provider — Instrukcja dla agenta AI

Provider key: "Adyen"

Sekcja konfiguracji: "Payments:Providers:Adyen"

Wymagane pola: ApiKey, MerchantAccount, NotificationHmacKey, ReturnUrl

Webhook: HMAC-SHA256 over eight fields of the item (pspReference:originalReference:merchantAccountCode:merchantReference:amount.value:amount.currency:eventCode:success), key NotificationHmacKey (hex), signature in additionalData.hmacSignature

Rejestracja: builder.Services.AddPayments().RegisterAdyenProvider();

Środowisko testowe: Environment = "test" (checkout-test.adyen.com)

Rules:
- Pass the raw JSON body in PaymentWebhookRequest.Body; the signature is in additionalData.hmacSignature, not in a header
- Answer HTTP 200 with the body "[accepted]" (AdyenProvider.Acknowledgement), for ignored events too
- Base decisions on the signed fields (references, amount, eventCode, success)
- Do not replace VerifyNotificationHmac with a stub returning true
```
