# TailoredApps.Shared.Payments.Provider.CashBill

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.CashBill)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.CashBill/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **CashBill** — polskim operatorem płatności online obsługującym przelewy, karty i BLIK.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.CashBill;

builder.Services
    .AddPayments()
    .RegisterCashbillProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Cashbill": {
        "ShopId": "MY_SHOP_ID",
        "ShopSecretPhrase": "my_secret_phrase",
        "ServiceUrl": "https://pay.cashbill.eu",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NegativeReturnUrl": "https://myapp.com/payment/failed"
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `ShopId` | tak | Identyfikator sklepu w CashBill |
| `ShopSecretPhrase` | tak | Fraza sekretna, którą podpisywane są żądania i weryfikowane powiadomienia |
| `ServiceUrl` | tak | URL REST API CashBill; nie ma wartości domyślnej. Gdy adres zawiera ścieżkę, zakończ go znakiem `/` — adresy żądań są wyznaczane względem niego |
| `ReturnUrl` | nie | URL powrotu po udanej płatności. Nie jest walidowany przy starcie, ale `RegisterPayment` kończy się błędem, jeśli nie jest to bezwzględny adres URL |
| `NegativeReturnUrl` | nie | URL powrotu po nieudanej/anulowanej płatności |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

CashBill zwraca dynamiczną listę kanałów pobraną z API dla danej waluty (PLN, EUR, USD itd.). Kanały obejmują przelew bankowy, karty, BLIK, e-portfele.

---

## Webhook

CashBill wysyła powiadomienie GET/POST z trzema parametrami w query stringu: `cmd`, `args` (identyfikator płatności) i `sign`.
Provider nie ma opcji z adresem powiadomień. Parametry czyta z `PaymentWebhookRequest.Query` (nie z body ani z nagłówków),
sprawdza, czy `sign` jest równe `MD5(cmd + args + ShopSecretPhrase)`, a następnie odczytuje bieżący status płatności
z API CashBill.

```csharp
[AcceptVerbs("GET", "POST", Route = "webhooks/cashbill")]
public async Task<IActionResult> CashBillWebhook(CancellationToken cancellationToken)
{
    var result = await _payments.HandleWebhookAsync("Cashbill", new PaymentWebhookRequest
    {
        HttpMethod = Request.Method,
        Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

| Powiadomienie | Wynik |
|---------------|-------|
| poprawny podpis | `PaymentWebhookProcessed` ze statusem odczytanym z CashBill (`Created`, `Processing`, `Finished` lub `Rejected`) |
| brak `args` | `PaymentWebhookRejected` (`MalformedPayload`) |
| brak lub niezgodność `sign` | `PaymentWebhookRejected` (`InvalidSignature`) |
| nieskonfigurowany `ShopSecretPhrase` | `PaymentWebhookRejected` (`NotConfigured`) |

---

## 🔒 Bezpieczeństwo

- Wywołania API idą przez `HttpClient` — nazwanego klienta `CashBill` (`CashbillHttpClient.HttpClientName`) z `IHttpClientFactory`, więc obowiązują je handlery, odporność i limity czasu skonfigurowane w hoście. Odpowiedź spoza 2xx zgłasza `PaymentGatewayException` (do 2.0.1: `WebException`) — łap `HttpRequestException`. Błędy są logowane z adresem bez query stringa, który zawiera podpis żądania.
- Podpis notyfikacji to `MD5(cmd + args + ShopSecretPhrase)`, porównywany w stałym czasie. Zarówno `HandleWebhookAsync`, jak i starsze `TransactionStatusChange` **weryfikują** podpis — nieprawidłowy podpis kończy się `PaymentWebhookRejected` (`InvalidSignature`, komunikat "Invalid signature.") / odpowiedzią `Rejected` bez odpytywania API CashBill.
- Komunikat błędu nie zawiera już oczekiwanego podpisu (wcześniej stanowił wyrocznię pozwalającą sfałszować dowolne powiadomienie).
- Brak `ShopSecretPhrase` → `PaymentWebhookRejected` (`NotConfigured`, komunikat "Signature verification is not configured.").
- `GetStatus(paymentId)` odrzuca identyfikatory ze znakami specjalnymi (`PaymentIdentifier.EnsureSafe`).
- Webhook z poprawnym podpisem zawsze daje `PaymentWebhookProcessed` ze statusem odczytanym z CashBill — nigdy `PaymentWebhookIgnored`. Przed działaniem sprawdź `Payment.PaymentStatus`: może to nadal być `Created` lub `Processing`.
- Po utworzeniu płatności provider zmienia jej adres powrotu na `{ReturnUrl}/{paymentId}`: płacący wraca na `ReturnUrl` z dopisanym identyfikatorem płatności.

---

## 🤖 AI Agent Prompt

```markdown
## CashBill Provider — Instrukcja dla agenta AI

Provider key: "Cashbill"

Sekcja konfiguracji: "Payments:Providers:Cashbill" (małe "b" w Cashbill!)

Wymagane pola: ShopId, ShopSecretPhrase, ServiceUrl

Rejestracja: builder.Services.AddPayments().RegisterCashbillProvider();
- Powiadomienie jest czytane z query stringa (cmd, args, sign): wypełnij PaymentWebhookRequest.Query
- Wynik webhooka to zawsze PaymentWebhookProcessed ze statusem odczytanym z CashBill — sprawdź Payment.PaymentStatus
- Nigdy nie zwracaj ErrorMessage webhooka klientowi; loguj po stronie serwera
- Sekret sklepu trzymaj w user-secrets/zmiennych środowiskowych — nigdy w repozytorium
```
