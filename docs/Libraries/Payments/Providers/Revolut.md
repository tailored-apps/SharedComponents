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
        "ApiKey": "your-merchant-api-key",
        "ApiUrl": "https://merchant.revolut.com/api",
        "WebhookSecret": "your-webhook-signing-secret",
        "WebhookToleranceSeconds": 300
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `ApiKey` | tak | Klucz API Revolut Merchant (wysyłany jako token `Bearer`) |
| `WebhookSecret` | tak | Sekret do weryfikacji podpisu webhooków |
| `ApiUrl` | nie | URL API Revolut Merchant (domyślnie `https://merchant.revolut.com/api`) |
| `WebhookToleranceSeconds` | nie | Największy akceptowany wiek znacznika czasu webhooka w sekundach, domyślnie `300`; `0` wyłącza sprawdzenie |
| `ReturnUrl` | nie | Wczytywany z konfiguracji, ale provider nie wysyła go do Revolut |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

Provider zwraca stałą listę dla każdej waluty: `card` (karta płatnicza), `revolut_pay` (Revolut Pay).

---

## Webhook

Revolut podpisuje powiadomienia w nagłówku HTTP `Revolut-Signature`, a czas podpisu wysyła w `Revolut-Request-Timestamp`.
Weryfikacja to HMAC-SHA256 z `WebhookSecret` liczony z surowego body, dlatego przekaż w `PaymentWebhookRequest` surowe body i wszystkie nagłówki.

| `event` | Wynik |
|---------|-------|
| `ORDER_COMPLETED` | `PaymentWebhookProcessed`, status `Finished` |
| `ORDER_CANCELLED`, `ORDER_PAYMENT_DECLINED`, `PAYMENT_DECLINED` | `PaymentWebhookProcessed`, status `Rejected` |
| `ORDER_AUTHORISED` lub inne zdarzenie | `PaymentWebhookIgnored` (status `Processing`) |

`PaymentUniqueId` w wyniku to `order_id` z powiadomienia.

---

## 🔒 Bezpieczeństwo

- Podpisywany ładunek to `v1.{Revolut-Request-Timestamp}.{surowe body}` (kropki — zgodnie z dokumentacją Revolut; poprzedni format `v1:` odrzucał każde prawdziwe powiadomienie).
- **Ochrona przed powtórką.** Znacznik czasu (milisekundy) musi mieścić się w `WebhookToleranceSeconds` (domyślnie 300 s) względem zegara serwera; `0` wyłącza sprawdzenie.
- Nagłówek `Revolut-Signature` może zawierać kilka podpisów (`v1=a,v1=b`) podczas rotacji sekretu — akceptowany jest dowolny pasujący.
- Pusty `WebhookSecret`, brak podpisu lub znacznika czasu → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed.

```json
{ "Payments": { "Providers": { "Revolut": { "WebhookSecret": "...", "WebhookToleranceSeconds": 300 } } } }
```

---

## 🤖 AI Agent Prompt

```markdown
## Revolut Provider — Instrukcja dla agenta AI

Provider key: "Revolut"

Sekcja konfiguracji: "Payments:Providers:Revolut"

Wymagane pola: ApiKey, WebhookSecret

Kwota: grosze/centy (int) — biblioteka konwertuje automatycznie

Rejestracja: builder.Services.AddPayments().RegisterRevolutProvider();
- Nie ustawiaj WebhookToleranceSeconds = 0 na produkcji — wyłącza ochronę przed powtórką powiadomień
```
