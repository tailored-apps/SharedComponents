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
        "ReturnUrl": "https://myapp.com/payment/return",
        "IsTest": true
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `SecretKey` | Klucz API Stripe (`sk_live_...` lub `sk_test_...`) |
| `WebhookSecret` | Sekret endpointu webhooka (`whsec_...`) |
| `ReturnUrl` | URL powrotu po zakończeniu Checkout Session |
| `IsTest` | `true` = tryb testowy (domyślnie) |

---

## Obsługiwane kanały

| Waluta | Kanały |
|--------|--------|
| PLN | `card` (Visa, Mastercard, Amex), `blik`, `p24` (Przelewy24) |
| EUR | `card`, `sepa_debit` |
| Inne | `card` |

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
public async Task<IActionResult> StripeWebhook()
{
    using var reader = new StreamReader(Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    var result = await _payments.HandleWebhookAsync("Stripe", new PaymentWebhookRequest
    {
        Body = rawBody,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
    });

    return result == PaymentWebhookResult.Fail ? BadRequest() : Ok();
}
```

!!! warning "Raw body"
    Do weryfikacji podpisu Stripe wymagany jest surowy body żądania. Nie używaj `[FromBody]` z automatyczną deserializacją JSON — zamiast tego czytaj `Request.Body` bezpośrednio.

---

## 🤖 AI Agent Prompt

```markdown
## Stripe Provider — Instrukcja dla agenta AI

Provider key: "Stripe"

Sekcja konfiguracji: "Payments:Providers:Stripe"

Wymagane pola: SecretKey, WebhookSecret, ReturnUrl

Przepływ: RegisterPayment → Checkout Session → redirect → webhook

Webhook: wymaga raw body (nie [FromBody]) + nagłówek Stripe-Signature

Sandbox: IsTest = true, SecretKey = "sk_test_..."

Rejestracja: builder.Services.AddPayments().RegisterStripeProvider();
```
