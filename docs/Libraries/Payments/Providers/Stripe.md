# TailoredApps.Shared.Payments.Provider.Stripe

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Stripe)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Stripe/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Stripe Checkout** — globalnym operatorem płatności kartą, BLIK (PLN) i Przelewy24. Provider używa Stripe Checkout Session (hosted page).

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Stripe
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Stripe;

builder.Services
    .AddPayments()
    .RegisterStripeProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Stripe": {
        "SecretKey": "sk_test_...",
        "WebhookSecret": "whsec_...",
        "SuccessUrl": "https://myapp.com/payment/success?session={CHECKOUT_SESSION_ID}",
        "CancelUrl": "https://myapp.com/payment/cancel"
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `SecretKey` | tak | Klucz API Stripe (`sk_live_...` lub `sk_test_...`) |
| `WebhookSecret` | tak | Sekret endpointu webhooka (`whsec_...`) |
| `SuccessUrl` | nie | URL powrotu po udanej płatności; Stripe podmienia `{CHECKOUT_SESSION_ID}` na identyfikator sesji |
| `CancelUrl` | nie | URL powrotu po anulowaniu lub nieudanej płatności |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane. Nie ma opcji
trybu testowego: tryb testowy oznacza użycie klucza `sk_test_...`.

Metody płatności sesji ustawia się osobną listą `Stripe:AllowedPaymentMethods` (na najwyższym poziomie konfiguracji,
nie pod `Payments:Providers:Stripe`). Gdy jest pusta, provider używa `card` i `blik` dla PLN, `card` i `sepa_debit` dla EUR
oraz `card` dla pozostałych walut.

---

## Obsługiwane kanały

Lista zwracana przez `GetChannels` jest stała w providerze:

| Waluta | Kanały |
|--------|--------|
| PLN | `card` (Visa, Mastercard, Amex), `blik`, `p24` (Przelewy24) |
| EUR | `card`, `sepa_debit` |
| Inne | `card` |

Metody faktycznie oferowane w sesji to te opisane w konfiguracji: `p24` nie należy do domyślnego zestawu dla PLN,
a `PaymentRequest.PaymentChannel` nie wybiera metody (trafia do metadanych sesji).

---

## Przepływ płatności

1. `RegisterPayment` → tworzy Stripe Checkout Session
2. Użytkownik jest przekierowany na `RedirectUrl` (hosted Stripe page)
3. Po płatności Stripe wywołuje webhook z eventami `checkout.session.completed` / `payment_intent.payment_failed`
4. Provider przetwarza webhook i zwraca `PaymentWebhookResult`

---

## Webhook

Stripe weryfikuje webhook przez `Stripe-Signature` w nagłówku. Niezbędny jest dostęp do **raw body** żądania HTTP.

```csharp
[HttpPost("webhooks/stripe")]
public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
{
    // Podpis obejmuje dokładne bajty body - czytaj je surowe, nie przez [FromBody].
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("Stripe", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

| Zdarzenie | Wynik |
|-----------|-------|
| `checkout.session.completed` z `payment_status` = `paid`, `checkout.session.async_payment_succeeded` | `PaymentWebhookProcessed`, status `Finished`, id sesji |
| `checkout.session.async_payment_failed`, `checkout.session.expired` | `PaymentWebhookProcessed`, status `Rejected`, id sesji |
| `payment_intent.succeeded` / `payment_intent.payment_failed` | `PaymentWebhookProcessed`, status `Finished` / `Rejected`, id payment intentu |
| `checkout.session.completed` jeszcze nieopłacone, inne zdarzenie, zdarzenie bez identyfikatora | `PaymentWebhookIgnored` |

!!! warning "Raw body"
    Do weryfikacji podpisu Stripe wymagany jest surowy body żądania. Nie używaj `[FromBody]` z automatyczną deserializacją JSON — zamiast tego czytaj `Request.Body` bezpośrednio.

---

## 🔒 Bezpieczeństwo

- Brak `WebhookSecret` lub nagłówka `Stripe-Signature` albo niezgodny podpis kończy się `PaymentWebhookRejected` (`InvalidSignature`) — Stripe.net nie jest już wywoływany z pustym kluczem.
- Zdarzenia `checkout.session.expired`, `checkout.session.async_payment_succeeded/failed` oraz `payment_intent.succeeded/payment_failed` niosą `PaymentUniqueId` (id sesji lub payment intentu).
- Zdarzenia `payment_intent.*` niosą id payment intentu, a nie id sesji zwrócone przez `RegisterPayment`. Aby dopasować webhooki do zapisanych id sesji, reaguj na zdarzenia `checkout.session.*`.

---

## 🤖 AI Agent Prompt

```markdown
## Stripe Provider — Instrukcja dla agenta AI

Provider key: "Stripe"

Sekcja konfiguracji: "Payments:Providers:Stripe"

Wymagane pola: SecretKey, WebhookSecret

Opcjonalne: SuccessUrl, CancelUrl; metody płatności w "Stripe:AllowedPaymentMethods" (sekcja na najwyższym poziomie)

Przepływ: RegisterPayment → Checkout Session → redirect → webhook

Webhook: wymaga raw body (nie [FromBody]) + nagłówek Stripe-Signature

Sandbox: SecretKey = "sk_test_..." (nie ma opcji IsTest)

Rejestracja: builder.Services.AddPayments().RegisterStripeProvider();
- WebhookSecret jest obowiązkowy — bez niego host nie wystartuje, a webhook, który nie przejdzie weryfikacji, kończy się PaymentWebhookRejected
- Zdarzenia payment_intent.* niosą id payment intentu, a nie id sesji — zapisane id sesji dopasowuj na zdarzeniach checkout.session.*
```
