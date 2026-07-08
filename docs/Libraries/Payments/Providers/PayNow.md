# TailoredApps.Shared.Payments.Provider.PayNow

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.PayNow)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.PayNow/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **PayNow** — bramką płatności mBanku obsługującą BLIK, szybkie przelewy i karty płatnicze.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayNow
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.PayNow;

builder.Services
    .AddPayments()
    .RegisterPayNowProvider();
```

---

## Konfiguracja `appsettings.json`

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

| Opcja | Opis |
|-------|------|
| `ApiKey` | Klucz API do autoryzacji żądań (header `Api-Key`) |
| `SignatureKey` | Klucz do podpisywania żądań i weryfikacji webhooków |
| `ServiceUrl` | URL API PayNow (sandbox: `https://api.sandbox.paynow.pl`) |
| `ReturnUrl` | URL powrotu po płatności |
| `ContinueUrl` | URL kontynuacji po powrocie ze strony PayNow |

---

## Obsługiwane kanały

PayNow obsługuje: BLIK, szybkie przelewy bankowe, karty płatnicze (PLN). Lista kanałów pobierana dynamicznie z API.

---

## Webhook

PayNow wysyła powiadomienia POST. Podpis weryfikowany przez SHA-256 HMAC z `SignatureKey`, przekazywany w nagłówku `Signature`.

---

## 🤖 AI Agent Prompt

```markdown
## PayNow Provider — Instrukcja dla agenta AI

Provider key: "PayNow"

Sekcja konfiguracji: "Payments:Providers:PayNow"

Wymagane pola: ApiKey, SignatureKey, ReturnUrl

Kwota w API: grosze (int), np. 1999 = 19.99 PLN — biblioteka konwertuje automatycznie

Sandbox URL: https://api.sandbox.paynow.pl

Rejestracja: builder.Services.AddPayments().RegisterPayNowProvider();
```
