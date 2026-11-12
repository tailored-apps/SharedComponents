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

## 🔒 Bezpieczeństwo

- Pusty `SecurityCode` lub pusty podpis → odrzucenie (fail-closed); porównanie w stałym czasie.
- ⚠️ **Znane ograniczenie.** Obecna weryfikacja (`SHA256(body + SecurityCode)` z nagłówka `X-Signature`) nie odpowiada protokołowi Tpay (formularz `application/x-www-form-urlencoded` z `md5sum = md5(id + tr_id + tr_amount + tr_crc + SecurityCode)` oraz nagłówek `X-JWS-Signature` z podpisem RS256). Prawdziwe powiadomienia są więc odrzucane — provider zachowuje się bezpiecznie, ale webhook nie działa produkcyjnie do czasu przepisania weryfikacji.

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
- Do czasu implementacji md5sum + X-JWS-Signature nie polegaj na webhooku Tpay w produkcji — sprawdzaj status przez GetStatus
```
