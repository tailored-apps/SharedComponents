# TailoredApps.Shared.Payments.Provider.PayU

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.PayU)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.PayU/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **PayU REST API v2.1** — wiodącym polskim operatorem płatności obsługującym BLIK, szybkie przelewy, karty i inne metody.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.PayU
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.PayU;

builder.Services
    .AddPayments()
    .RegisterPayUProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "PayU": {
        "ClientId": "your-client-id",
        "ClientSecret": "your-client-secret",
        "PosId": "your-pos-id",
        "SignatureKey": "your-signature-key",
        "ServiceUrl": "https://secure.snd.payu.com",
        "NotifyUrl": "https://myapp.com/webhooks/payu",
        "ContinueUrl": "https://myapp.com/payment/return"
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `ClientId` | tak | Identyfikator klienta OAuth |
| `ClientSecret` | tak | Sekret klienta OAuth |
| `PosId` | tak | Identyfikator punktu sprzedaży (merchantPosId) |
| `SignatureKey` | tak | Klucz do weryfikacji podpisu powiadomień (second key) |
| `ServiceUrl` | nie | URL API PayU. Wartością domyślną jest sandbox, `https://secure.snd.payu.com` — dla prawdziwych płatności ustaw adres produkcyjny |
| `NotifyUrl` | nie | URL do powiadomień o transakcjach (webhook) |
| `ContinueUrl` | nie | URL powrotu po płatności |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

Lista jest stała w providerze:

| Waluta | Kanały |
|--------|--------|
| PLN | `blik` (BLIK), `c` (karta płatnicza), `o` (przelew online), `ai` (raty), `ap` (Apple Pay), `jp` (Google Pay) |
| Inne | `c` |

---

## Autoryzacja

Provider pobiera token OAuth2 (`client_credentials`) przy każdym wywołaniu `RegisterPayment` i `GetStatus`. Token nie jest cache'owany.

---

## Webhook

PayU wysyła powiadomienia POST z nagłówkiem `OpenPayU-Signature` (`signature=...;algorithm=...`). Provider liczy skrót z surowego body
i dopisanego `SignatureKey` algorytmem wskazanym w nagłówku — `MD5`, `SHA-1` lub `SHA-256` (`MD5`, gdy nagłówek go nie wskazuje) —
i porównuje wynik z `signature`.

```csharp
[HttpPost("webhooks/payu")]
public async Task<IActionResult> PayUWebhook(CancellationToken cancellationToken)
{
    // Podpis obejmuje dokładne bajty body - czytaj je surowe, nie przez [FromBody].
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("PayU", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

| `order.status` | Wynik |
|----------------|-------|
| `COMPLETED` | `PaymentWebhookProcessed`, status `Finished` |
| `CANCELED`, `REJECTED` | `PaymentWebhookProcessed`, status `Rejected` |
| inna wartość / brak | `PaymentWebhookIgnored` (status `Processing`) |

---

## 🔒 Bezpieczeństwo

- Zamówienia są tworzone przez nazwanego klienta HTTP `PayU.Orders` (`PayUServiceCaller.OrdersClientName`), pozostałe wywołania przez `PayU`. `RegisterPayUProvider()` konfiguruje `PayU.Orders` bez podążania za przekierowaniami i bez ciasteczek: PayU odpowiada na zamówienie kodem 302, a identyfikator zamówienia jest w treści tej odpowiedzi. Jeśli konfigurujesz tego klienta samodzielnie, zostaw `AllowAutoRedirect = false` — inaczej provider zgłosi `InvalidOperationException` zamiast zgubić identyfikator.
- Domyślny `ServiceUrl` to sandbox PayU (`https://secure.snd.payu.com`) — adres produkcyjny ustaw jawnie.
- Każde zamówienie jest wysyłane z `customerIp` = `127.0.0.1`; adres płacącego nie jest przekazywany do PayU.
- Pusty `SignatureKey` lub brak `signature=` w nagłówku `OpenPayU-Signature` → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed; porównanie w stałym czasie.
- Obsługiwane algorytmy: `MD5`, `SHA-1`, `SHA-256` (zgodnie z ustawieniem POS w panelu PayU); inne wartości `algorithm=` są odrzucane. Zalecany SHA-256.
- Wynik webhooka niesie `PaymentUniqueId` (`order.orderId`).

---

## 🤖 AI Agent Prompt

```markdown
## PayU Provider — Instrukcja dla agenta AI

Provider key: "PayU"

Sekcja konfiguracji: "Payments:Providers:PayU"

Wymagane pola: ClientId, ClientSecret, PosId, SignatureKey

Autoryzacja: OAuth2 client_credentials — automatyczna, token pobierany przy każdym wywołaniu (bez cache)

Kwota w API: grosze (int) — biblioteka konwertuje automatycznie z decimal

Sandbox: ServiceUrl = "https://secure.snd.payu.com" (to wartość domyślna — adres produkcyjny ustaw jawnie)

Rejestracja: builder.Services.AddPayments().RegisterPayUProvider();
- Ustaw w panelu PayU algorytm SHA-256 dla podpisu powiadomień
- Webhook: przekaż surowe body (nie [FromBody]) i wszystkie nagłówki żądania; na wynik PaymentWebhookRejected odpowiedz statusem spoza 2xx
```
