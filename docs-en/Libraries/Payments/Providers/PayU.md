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

| Opcja | Opis |
|-------|------|
| `ClientId` | Identyfikator klienta OAuth |
| `ClientSecret` | Sekret klienta OAuth |
| `PosId` | Identyfikator punktu sprzedaży (merchantPosId) |
| `SignatureKey` | Klucz do podpisu powiadomień (second key) |
| `ServiceUrl` | URL API PayU (sandbox: `https://secure.snd.payu.com`) |
| `NotifyUrl` | URL do powiadomień o transakcjach (webhook) |
| `ContinueUrl` | URL powrotu po płatności |

---

## Obsługiwane kanały

PayU oferuje szeroką gamę kanałów dla PLN i EUR: BLIK, przelewy bankowe, karty, PayPal, Google Pay, Apple Pay. Lista pobierana dynamicznie z API PayU.

---

## Autoryzacja

Provider automatycznie pobiera token OAuth2 (`client_credentials`) przed każdym żądaniem i cache'uje go do wygaśnięcia.

---

## Webhook

PayU wysyła powiadomienia POST z nagłówkiem `OpenPayU-Signature`. Podpis weryfikowany przez MD5 z `SignatureKey`.

```csharp
[HttpPost("webhooks/payu")]
public async Task<IActionResult> PayUWebhook([FromBody] JsonElement body)
{
    var result = await _payments.HandleWebhookAsync("PayU", new PaymentWebhookRequest
    {
        Body = body.GetRawText(),
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
    });
    return Ok(); // PayU oczekuje 200 OK nawet przy błędzie weryfikacji
}
```

---

## 🤖 AI Agent Prompt

```markdown
## PayU Provider — Instrukcja dla agenta AI

Provider key: "PayU"

Sekcja konfiguracji: "Payments:Providers:PayU"

Wymagane pola: ClientId, ClientSecret, PosId, SignatureKey, NotifyUrl, ContinueUrl

Autoryzacja: OAuth2 client_credentials — automatyczna, nie wymaga interwencji

Kwota w API: grosze (int) — biblioteka konwertuje automatycznie z decimal

Sandbox: ServiceUrl = "https://secure.snd.payu.com"

Rejestracja: builder.Services.AddPayments().RegisterPayUProvider();
```
