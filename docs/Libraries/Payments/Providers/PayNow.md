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

| Opcja | Wymagana | Opis |
|-------|------|------|
| `ApiKey` | tak | Klucz API do autoryzacji żądań (header `Api-Key`) |
| `SignatureKey` | tak | Klucz do weryfikacji podpisów webhooków |
| `ServiceUrl` | nie | URL API PayNow, domyślnie `https://api.paynow.pl` (sandbox: `https://api.sandbox.paynow.pl`). Dawny alias `ApiUrl` usunięto w 3.0: konfiguracja, która go jeszcze ustawia, zatrzymuje start hosta, zamiast po cichu przejść na produkcję |
| `ReturnUrl` | nie | URL powrotu po płatności |
| `ContinueUrl` | nie | URL kontynuacji po powrocie ze strony PayNow |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

Provider zwraca stałą listę dla każdej waluty: `BLIK`, `CARD` (karta płatnicza), `PBL` (przelew pay-by-link), `TRANSFER` (przelew tradycyjny).

---

## Webhook

PayNow wysyła powiadomienia POST. Podpis weryfikowany przez SHA-256 HMAC z `SignatureKey`, przekazywany w nagłówku `Signature`.

---

## 🔒 Bezpieczeństwo

- Pusty `SignatureKey` lub pusty podpis → odrzucenie (fail-closed); porównanie w stałym czasie.
- Nagłówek `Idempotency-Key` jest stały dla tego samego `AdditionalData` (externalId), więc ponowione żądanie nie tworzy drugiej płatności.
- Status `EXPIRED` mapuje się na `Rejected`; wynik webhooka niesie `PaymentUniqueId` (`paymentId`).

---

## 🤖 AI Agent Prompt

```markdown
## PayNow Provider — Instrukcja dla agenta AI

Provider key: "PayNow"

Sekcja konfiguracji: "Payments:Providers:PayNow"

Wymagane pola: ApiKey, SignatureKey

Kwota w API: grosze (int), np. 1999 = 19.99 PLN — biblioteka konwertuje automatycznie

Sandbox URL: https://api.sandbox.paynow.pl

Rejestracja: builder.Services.AddPayments().RegisterPayNowProvider();
- Przekazuj w AdditionalData stabilny identyfikator zamówienia — służy jako Idempotency-Key i externalId
```
