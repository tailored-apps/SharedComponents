# TailoredApps.Shared.Payments.Provider.Revolut

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Revolut)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Revolut/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Revolut Merchant API** — obsługa płatności kartą i Revolut Pay z weryfikacją webhooków przez podpis.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Revolut
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Revolut;

builder.Services
    .AddPayments()
    .RegisterRevolutProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Revolut": {
        "ApiKey": "sk_test_...",
        "ApiUrl": "https://merchant.revolut.com/api",
        "ReturnUrl": "https://myapp.com/payment/return",
        "WebhookSecret": "whsec_..."
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `ApiKey` | Klucz API Revolut Merchant (`sk_live_...` lub `sk_test_...`) |
| `ApiUrl` | URL API Revolut Merchant |
| `ReturnUrl` | URL powrotu po płatności |
| `WebhookSecret` | Sekret do weryfikacji podpisu webhooków (`whsec_...`) |

---

## Obsługiwane kanały

Revolut obsługuje płatności kartą (Visa, Mastercard) i Revolut Pay dla wielu walut (PLN, EUR, GBP, USD i inne).

---

## Webhook

Revolut podpisuje powiadomienia podpisem `Revolut-Signature` w nagłówku HTTP. Weryfikacja przez HMAC-SHA256 z `WebhookSecret`.

---

## 🤖 AI Agent Prompt

```markdown
## Revolut Provider — Instrukcja dla agenta AI

Provider key: "Revolut"

Sekcja konfiguracji: "Payments:Providers:Revolut"

Wymagane pola: ApiKey, ReturnUrl, WebhookSecret

Kwota: grosze/centy (int) — biblioteka konwertuje automatycznie

Test API key: sk_test_...

Rejestracja: builder.Services.AddPayments().RegisterRevolutProvider();
```
