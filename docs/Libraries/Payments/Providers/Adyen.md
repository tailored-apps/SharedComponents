# TailoredApps.Shared.Payments.Provider.Adyen

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Adyen)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Adyen/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Adyen Checkout API**. Obsługuje płatności kartą, BLIK (PLN) i inne metody dostępne w Adyen, z pełną obsługą webhooków i weryfikacją HMAC.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Adyen
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Adyen;

builder.Services
    .AddPayments()
    .RegisterAdyenProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Adyen": {
        "ApiKey": "AQEyhmf...",
        "MerchantAccount": "MyCompanyECOM",
        "ClientKey": "test_...",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotificationHmacKey": "446a32...hex...",
        "Environment": "test"
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `ApiKey` | tak | Klucz API Adyen (header `X-API-Key`) |
| `MerchantAccount` | tak | Identyfikator konta merchant |
| `NotificationHmacKey` | tak | Klucz HMAC (hex) do weryfikacji webhooków |
| `ReturnUrl` | nie | URL powrotu po płatności |
| `Environment` | nie | `"test"` (domyślnie) wywołuje `https://checkout-test.adyen.com/v71`; każda inna wartość, np. `"live"`, wywołuje `https://checkout-live.adyen.com/v71` |
| `CheckoutUrl` | nie | Pełny bazowy URL Checkout API; gdy ustawiony, zastępuje adres wynikający z `Environment` |
| `ClientKey` | nie | Klucz klienta Drop-in/Components — wczytywany z konfiguracji, ale nieużywany przez providera |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały płatności

Lista jest stała w providerze:

| Waluta | Kanały |
|--------|--------|
| PLN | `scheme` (karta płatnicza: Visa, Mastercard), `blik`, `onlineBanking_PL` (przelew online) |
| EUR | `scheme`, `ideal`, `sepadirectdebit` |
| Inne | `scheme` |

---

## Płatność i odczyt wyniku

Provider tworzy sesję **Hosted Checkout** (`POST /sessions` z `mode: hosted`): `RegisterPayment` zwraca identyfikator sesji
w `PaymentUniqueId` i adres strony płatności Adyen w `RedirectUrl`.

```csharp
// 1. utwórz płatność i przekieruj kupującego na stronę Adyen
var response = await _payments.RegisterPayment(new PaymentRequest("Adyen", 49.99m, "PLN")
{
    AdditionalData = orderNumber,   // wróci jako merchantReference w webhooku
}, cancellationToken);
await _orders.SavePaymentSessionAsync(orderNumber, response.PaymentUniqueId, cancellationToken);
return Redirect(response.RedirectUrl);

// 2. kupujący wraca na ReturnUrl; Adyen dopisuje do adresu sessionId i sessionResult
[HttpGet("payment/return")]
public async Task<IActionResult> Return(string sessionId, string sessionResult, CancellationToken cancellationToken)
{
    var payment = await _payments.GetStatus("Adyen", AdyenProvider.SessionResultId(sessionId, sessionResult), cancellationToken);
    return payment.PaymentStatus == PaymentStatusEnum.Finished ? View("Paid") : View("Pending");
}
```

| Status sesji Adyen | `PaymentStatus` |
|--------------------|-----------------|
| `completed` | `Finished` |
| `paymentPending` | `Processing` |
| `refused`, `canceled`, `expired` | `Rejected` |
| `active` (kupujący jeszcze nie zapłacił) | `Created` |

- Adyen podaje wynik sesji **tylko** razem z wartością `sessionResult`, którą dopisuje do adresu powrotu. `GetStatus` z samym
  identyfikatorem sesji kończy się `ArgumentException` — poza powrotem kupującego jedynym źródłem wyniku jest [webhook](#webhook).
- Wynik odczytany przy powrocie służy do pokazania strony z podziękowaniem; zamówienie rozliczaj na podstawie webhooka.
- Zakres weryfikacji: żądanie i odpowiedź są zgodne ze specyfikacją OpenAPI Checkout v71 (pola `mode`, `url`,
  `GET /sessions/{sessionId}?sessionResult=`); nazwy parametrów dopisywanych do adresu powrotu pochodzą z dokumentacji Hosted Checkout
  i — jak cały provider — nie były sprawdzone w środowisku testowym Adyen.

---

## Webhook

Standardowy webhook Adyen to JSON z tablicą `notificationItems`; każdy element `NotificationRequestItem` niesie własny podpis
w `additionalData.hmacSignature` — **nie ma nagłówka z podpisem**. Podpis to HMAC-SHA256 (klucz: `NotificationHmacKey` zdekodowany z hex,
wynik w Base64) z ośmiu pól elementu połączonych dwukropkiem; brakujące pole to pusty tekst:

```text
pspReference:originalReference:merchantAccountCode:merchantReference:amount.value:amount.currency:eventCode:success
```

W panelu Adyen (Developers → Webhooks) włącz podpis HMAC dla webhooka i skopiuj wygenerowany klucz do `NotificationHmacKey`.

```csharp
[HttpPost("webhooks/adyen")]
public async Task<IActionResult> AdyenWebhook(CancellationToken cancellationToken)
{
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("Adyen", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    // Adyen ponawia powiadomienie, dopóki nie dostanie odpowiedzi 2xx.
    return result is PaymentWebhookRejected ? Unauthorized() : Content(AdyenProvider.Acknowledgement, "text/plain");
}
```

| `eventCode` | `success` | Wynik |
|-------------|-----------|-------|
| `AUTHORISATION`, `REFUND` | `true` | `PaymentWebhookProcessed`, status `Finished` |
| `AUTHORISATION`, `REFUND` | inna wartość / brak | `PaymentWebhookProcessed`, status `Rejected` |
| `CANCELLATION`, `AUTHORISATION_FAILED` | dowolna | `PaymentWebhookProcessed`, status `Rejected` |
| inne zdarzenie | dowolna | `PaymentWebhookIgnored` (status `Processing`) — nadal potwierdź odbiór |

---

## 🔒 Bezpieczeństwo

- Weryfikacja jest fail-closed: pusty lub niebędący hex `NotificationHmacKey`, brak `additionalData.hmacSignature` albo niezgodny podpis →
  `PaymentWebhookRejection.InvalidSignature`; body niebędące JSON-em lub bez `NotificationRequestItem` → `MalformedPayload`. Porównanie w stałym czasie.
- Podpis obejmuje osiem pól, a nie całe body: pozostałe pola elementu (np. `paymentMethod`, `reason`) **nie są uwierzytelnione** — nie opieraj na nich decyzji.
- Brak pola `success` jest traktowany jako **niepowodzenie**.
- Wynik webhooka niesie `PaymentUniqueId` (`merchantReference`, a w razie braku `pspReference`) oraz `ResponseObject` = `[accepted]`.
- Obsługiwany jest pierwszy element `notificationItems` (Adyen wysyła jeden element na żądanie).
- Zakres weryfikacji: implementacja odtwarza przykład z dokumentacji Adyen (klucz, podpisywany tekst i oczekiwany podpis są w testach);
  przed wdrożeniem wyślij testowe powiadomienie z panelu Adyen.

---

## 🤖 AI Agent Prompt

```markdown
## Adyen Provider — Instrukcja dla agenta AI

Provider key: "Adyen"

Sekcja konfiguracji: "Payments:Providers:Adyen"

Wymagane pola: ApiKey, MerchantAccount, NotificationHmacKey

Webhook: HMAC-SHA256 z ośmiu pól elementu (pspReference:originalReference:merchantAccountCode:merchantReference:amount.value:amount.currency:eventCode:success), klucz NotificationHmacKey (hex), podpis w additionalData.hmacSignature

Rejestracja: builder.Services.AddPayments().RegisterAdyenProvider();

Środowisko testowe: Environment = "test" (checkout-test.adyen.com)

Zasady:
- RegisterPayment zwraca RedirectUrl (strona Hosted Checkout) i PaymentUniqueId (id sesji)
- Status po powrocie kupującego: GetStatus("Adyen", AdyenProvider.SessionResultId(sessionId, sessionResult)) — oba parametry są w adresie powrotu; sam id sesji nie wystarcza
- Przekaż do PaymentWebhookRequest.Body surowe body JSON; podpis jest w additionalData.hmacSignature, nie w nagłówku
- Odpowiedz HTTP 200 z treścią "[accepted]" (AdyenProvider.Acknowledgement) także dla zdarzeń zignorowanych
- Decyzje opieraj na polach objętych podpisem (referencje, kwota, eventCode, success)
- Nie zastępuj VerifyNotificationHmac stubem zwracającym true
```
