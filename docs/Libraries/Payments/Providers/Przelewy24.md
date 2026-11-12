# TailoredApps.Shared.Payments.Provider.Przelewy24

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Przelewy24)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Przelewy24/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Przelewy24** — jednym z najpopularniejszych polskich operatorów płatności online obsługującym ponad 170 banków i BLIK.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Przelewy24
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Przelewy24;

builder.Services
    .AddPayments()
    .RegisterPrzelewy24Provider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Przelewy24": {
        "MerchantId": 12345,
        "PosId": 12345,
        "ApiKey": "your-api-key",
        "CrcKey": "your-crc-key",
        "ServiceUrl": "https://secure.przelewy24.pl",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/przelewy24"
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `MerchantId` | Identyfikator sprzedawcy w Przelewy24 |
| `PosId` | Identyfikator punktu sprzedaży (zazwyczaj = MerchantId) |
| `ApiKey` | Klucz API do autoryzacji żądań |
| `CrcKey` | Klucz CRC do generowania podpisów transakcji |
| `ServiceUrl` | URL API (sandbox: `https://sandbox.przelewy24.pl`) |
| `ReturnUrl` | URL powrotu po płatności |
| `NotifyUrl` | URL do powiadomień o transakcjach (webhook) |

---

## Obsługiwane kanały

Przelewy24 obsługuje PLN i EUR przez ponad 170 kanałów płatności (banki, BLIK, karty). Lista kanałów pobierana z API Przelewy24.

---

## Webhook

Przelewy24 weryfikuje płatność przez endpoint `/transaction/verify`. Podpis transakcji oparty na SHA-384 z kluczem CRC.

---

## 🔒 Bezpieczeństwo

- Podpis notyfikacji jest liczony jako SHA-384 z JSON `{"merchantId","posId","sessionId","amount","originAmount","currency","orderId","methodId","statement","crc"}` w tej kolejności (dokumentacja P24), z niezmienionymi znakami unicode i ukośnikami. Podpis wywołania `verify` to `{"sessionId","orderId","amount","currency","crc"}`.
- Pusty `CrcKey` lub brak pola `sign` → odrzucenie (fail-closed); porównanie w stałym czasie.
- `PaymentUniqueId` w wyniku webhooka to `sessionId`.

---

## 🤖 AI Agent Prompt

```markdown
## Przelewy24 Provider — Instrukcja dla agenta AI

Provider key: "Przelewy24"

Sekcja konfiguracji: "Payments:Providers:Przelewy24"

Wymagane pola: MerchantId, PosId, ApiKey, CrcKey, ReturnUrl, NotifyUrl

Kwota: grosze (int) — biblioteka konwertuje automatycznie

Sandbox: ServiceUrl = "https://sandbox.przelewy24.pl"

Rejestracja: builder.Services.AddPayments().RegisterPrzelewy24Provider();
- Nie modyfikuj kolejności pól w podpisie — P24 liczy hash z JSON dokładnie w udokumentowanej kolejności
```
