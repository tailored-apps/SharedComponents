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

Adyen wysyła powiadomienia na endpoint HTTP. Weryfikacja odbywa się przez HMAC SHA-256 na podstawie `NotificationHmacKey`.

```csharp
[HttpPost("webhooks/adyen")]
public async Task<IActionResult> AdyenWebhook([FromBody] JsonElement body)
{
    var result = await _payments.HandleWebhookAsync("Adyen", new PaymentWebhookRequest
    {
        Body = body.GetRawText(),
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
    });
    return result == PaymentWebhookResult.Success ? Ok("[accepted]") : BadRequest();
}
```

---

## 🤖 AI Agent Prompt

```markdown
## Adyen Provider — Instrukcja dla agenta AI

Provider key: "Adyen"

Sekcja konfiguracji: "Payments:Providers:Adyen"

Wymagane pola: ApiKey, MerchantAccount, ReturnUrl

Webhook: HMAC-SHA256, klucz NotificationHmacKey

Rejestracja: builder.Services.AddPayments().RegisterAdyenProvider();

Środowisko testowe: Environment = "test" (checkout-test.adyen.com)
```
