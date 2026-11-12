# TailoredApps.Shared.Payments.Provider.Tpay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Tpay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Tpay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Tpay** — polskim operatorem płatności online obsługującym szybkie przelewy, BLIK, karty i inne metody.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Tpay
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Tpay;

builder.Services
    .AddPayments()
    .RegisterTpayProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Tpay": {
        "ClientId": "your-client-id",
        "ClientSecret": "your-client-secret",
        "MerchantId": "your-merchant-id",
        "ServiceUrl": "https://api.tpay.com",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/tpay",
        "SecurityCode": "your-security-code"
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `ClientId` | Identyfikator klienta OAuth2 |
| `ClientSecret` | Sekret klienta OAuth2 |
| `MerchantId` | Identyfikator sprzedawcy |
| `ServiceUrl` | URL API Tpay (sandbox: `https://openapi.sandbox.tpay.com`) |
| `ReturnUrl` | URL powrotu po płatności |
| `NotifyUrl` | URL do powiadomień (webhook) |
| `SecurityCode` | Kod bezpieczeństwa do weryfikacji powiadomień |

---

## Obsługiwane kanały

Tpay obsługuje PLN przez: szybkie przelewy bankowe, BLIK, karty, Google Pay, Apple Pay. Lista kanałów pobierana z API.

---

## Autoryzacja

Provider automatycznie pobiera token OAuth2 przez endpoint `/oauth/auth` i odświeża go po wygaśnięciu.

---

## Webhook

Tpay wysyła powiadomienia POST z podpisem weryfikowanym przez MD5 z `SecurityCode`.

---

## 🔒 Security

- Empty `SecurityCode` or an empty signature → rejected (fail-closed); constant-time comparison.
- ⚠️ **Known limitation.** The current verification (`SHA256(body + SecurityCode)` from an `X-Signature` header) does not match the Tpay protocol (an `application/x-www-form-urlencoded` form with `md5sum = md5(id + tr_id + tr_amount + tr_crc + SecurityCode)` plus an RS256 `X-JWS-Signature` header). Genuine notifications are therefore rejected - the provider fails safe, but the webhook is not production-usable until verification is rewritten.

---

## 🤖 AI Agent Prompt

```markdown
## Tpay Provider — Instrukcja dla agenta AI

Provider key: "Tpay"

Sekcja konfiguracji: "Payments:Providers:Tpay"

Wymagane pola: ClientId, ClientSecret, MerchantId, ReturnUrl, NotifyUrl, SecurityCode

Autoryzacja: OAuth2 — automatyczna, token cache'owany

Sandbox: ServiceUrl = "https://openapi.sandbox.tpay.com"

Rejestracja: builder.Services.AddPayments().RegisterTpayProvider();
- Until md5sum + X-JWS-Signature verification is implemented, do not rely on the Tpay webhook in production - poll GetStatus instead
```
