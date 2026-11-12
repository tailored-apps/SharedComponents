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

| Opcja | Wymagana | Opis |
|-------|------|------|
| `MerchantId` | tak | Identyfikator sprzedawcy w Przelewy24 (liczba większa od 0) |
| `PosId` | tak | Identyfikator punktu sprzedaży (zazwyczaj = MerchantId; liczba większa od 0) |
| `ApiKey` | tak | Klucz API do autoryzacji żądań |
| `CrcKey` | tak | Klucz CRC do podpisów transakcji i weryfikacji powiadomień |
| `ServiceUrl` | nie | URL API, domyślnie `https://secure.przelewy24.pl` (sandbox: `https://sandbox.przelewy24.pl`) |
| `ReturnUrl` | nie | URL powrotu po płatności |
| `NotifyUrl` | nie | URL do powiadomień o transakcjach (webhook) |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

Provider zwraca stałą listę dla każdej waluty: `online_transfer` (przelew online), `blik` (BLIK), `card` (karta płatnicza).

---

## Webhook

Przelewy24 wysyła powiadomienie JSON metodą POST na `NotifyUrl`. Podpisem jest pole `sign` w body (SHA-384 z kluczem CRC); nie ma
nagłówka z podpisem, dlatego przekaż surowe body w `PaymentWebhookRequest.Body`. Po sprawdzeniu podpisu provider potwierdza
transakcję przez endpoint `/transaction/verify` i dopiero wtedy zwraca wynik:

| Przebieg | Wynik |
|----------|-------|
| Przelewy24 przyjmuje weryfikację | `PaymentWebhookProcessed`, status `Finished` |
| Przelewy24 odrzuca weryfikację (4xx) | `PaymentWebhookProcessed`, status `Rejected` |
| wywołanie weryfikacji kończy się błędem (5xx, 408, 429) lub powiadomienia nie da się przetworzyć | `PaymentWebhookRejected` (`ProcessingFailed`) |
| brak `sessionId`, `amount`, `currency` lub `orderId` | `PaymentWebhookRejected` (`MalformedPayload`) |
| brak lub niezgodność `sign` | `PaymentWebhookRejected` (`InvalidSignature`) |

Na wynik `PaymentWebhookRejected` odpowiedz statusem spoza 2xx — Przelewy24 wyśle wtedy powiadomienie ponownie.

---

## 🔒 Bezpieczeństwo

- Podpis notyfikacji jest liczony jako SHA-384 z JSON `{"merchantId","posId","sessionId","amount","originAmount","currency","orderId","methodId","statement","crc"}` w tej kolejności (dokumentacja P24), z niezmienionymi znakami unicode i ukośnikami. Podpis wywołania `verify` to `{"sessionId","orderId","amount","currency","crc"}`.
- Pusty `CrcKey` lub brak pola `sign` → odrzucenie (fail-closed); porównanie w stałym czasie.
- `PaymentUniqueId` w wyniku webhooka to `sessionId`.
- `GetStatus` nie odpytuje Przelewy24 i zawsze zwraca `Processing` — wynik płatności pochodzi wyłącznie z webhooka.

---

## 🤖 AI Agent Prompt

```markdown
## Przelewy24 Provider — Instrukcja dla agenta AI

Provider key: "Przelewy24"

Sekcja konfiguracji: "Payments:Providers:Przelewy24"

Wymagane pola: MerchantId, PosId, ApiKey, CrcKey

Kwota: grosze (int) — biblioteka konwertuje automatycznie

Sandbox: ServiceUrl = "https://sandbox.przelewy24.pl"

Rejestracja: builder.Services.AddPayments().RegisterPrzelewy24Provider();
- Nie modyfikuj kolejności pól w podpisie — P24 liczy hash z JSON dokładnie w udokumentowanej kolejności
- GetStatus zawsze zwraca Processing bez odpytywania Przelewy24 — polegaj na webhooku
```
