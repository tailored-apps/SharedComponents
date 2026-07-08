# TailoredApps.Shared.Payments.Provider.HotPay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.HotPay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.HotPay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **HotPay** — polskim mikropłatnościowym operatorem płatności online.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.HotPay
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.HotPay;

builder.Services
    .AddPayments()
    .RegisterHotPayProvider();
```

---

## Konfiguracja `appsettings.json`

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

| Opcja | Opis |
|-------|------|
| `SecretHash` | Sekret do podpisywania żądań (SEKRET) |
| `ServiceUrl` | URL API HotPay |
| `ReturnUrl` | URL powrotu po płatności |
| `NotifyUrl` | URL do powiadomień o zmianie statusu |

---

## Obsługiwane kanały

HotPay obsługuje płatności online (PLN). Kanał domyślny: `hotpay`.

---

## Webhook

HotPay wysyła powiadomienie POST na `NotifyUrl`. Podpis weryfikowany przez SHA-256 z `SecretHash`.

---

## 🤖 AI Agent Prompt

```markdown
## HotPay Provider — Instrukcja dla agenta AI

Provider key: "HotPay"

Sekcja konfiguracji: "Payments:Providers:HotPay"

Wymagane pola: SecretHash, ReturnUrl, NotifyUrl

Rejestracja: builder.Services.AddPayments().RegisterHotPayProvider();
```
