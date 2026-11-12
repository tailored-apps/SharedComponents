# TailoredApps.Shared.Payments.Provider.HotPay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.HotPay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.HotPay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **HotPay** — polskim mikropłatnościowym operatorem płatności online.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.HotPay
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.HotPay;

builder.Services
    .AddPayments()
    .RegisterHotPayProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "HotPay": {
        "SecretHash": "my_secret_hash",
        "ServiceUrl": "https://platnosci.hotpay.pl",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/hotpay"
      }
    }
  }
}
```

| Opcja | Wymagana | Opis |
|-------|------|------|
| `SecretHash` | tak | Sekret HotPay (`SEKRET`): podpisuje żądanie płatności i weryfikuje powiadomienia |
| `ServiceUrl` | nie | URL API HotPay (domyślnie `https://platnosci.hotpay.pl`) |
| `ReturnUrl` | nie | URL powrotu po płatności (`ADRES_WWW`) |
| `NotifyUrl` | nie | Wczytywany z konfiguracji, ale provider nie wysyła go do HotPay |

Opcje wymagane są sprawdzane przy starcie hosta (`OptionsValidationException`); pozostałe nie są walidowane.

---

## Obsługiwane kanały

Provider zwraca stałą listę dla każdej waluty: `blik` (BLIK), `card` (karta płatnicza), `transfer` (przelew online).

---

## Webhook

Provider czyta cztery parametry powiadomienia z query stringa (`PaymentWebhookRequest.Query`): `HASH`, `KWOTA`,
`ID_PLATNOSCI` i `STATUS`. Nie czyta body ani nagłówków. `HASH` musi być równy SHA-256 (hex) z
`SecretHash;KWOTA;ID_PLATNOSCI;STATUS`.

```csharp
[HttpPost("webhooks/hotpay")]
public async Task<IActionResult> HotPayWebhook(CancellationToken cancellationToken)
{
    var result = await _payments.HandleWebhookAsync("HotPay", new PaymentWebhookRequest
    {
        Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
    }, cancellationToken);

    return result is PaymentWebhookRejected ? BadRequest() : Ok();
}
```

Jeśli wartości docierają do endpointu jako pola formularza, a nie parametry query stringa, przepisz je do `Query`.

| `STATUS` | Wynik |
|----------|-------|
| `SUCCESS` | `PaymentWebhookProcessed`, status `Finished` |
| inna wartość | `PaymentWebhookProcessed`, status `Rejected` |

`PaymentUniqueId` w wyniku to `ID_PLATNOSCI`.

---

## 🔒 Bezpieczeństwo

- Pusty `SecretHash` albo pusty lub niezgodny `HASH` → `PaymentWebhookRejected` (`InvalidSignature`), fail-closed; porównanie w stałym czasie.
- Podpisywana nazwa usługi (`NAZWA_USLUGI`) jest dokładnie tą, która trafia do żądania inicjującego płatność.
- `GetStatus` nie odpytuje HotPay i zawsze zwraca `Processing` — wynik płatności pochodzi wyłącznie z webhooka.

---

## 🤖 AI Agent Prompt

```markdown
## HotPay Provider — Instrukcja dla agenta AI

Provider key: "HotPay"

Sekcja konfiguracji: "Payments:Providers:HotPay"

Wymagane pola: SecretHash

Rejestracja: builder.Services.AddPayments().RegisterHotPayProvider();
- Powiadomienie jest czytane z query stringa (HASH, KWOTA, ID_PLATNOSCI, STATUS): wypełnij PaymentWebhookRequest.Query
- GetStatus zawsze zwraca Processing bez odpytywania HotPay — polegaj na webhooku
- SecretHash trzymaj poza repozytorium; bez niego każde powiadomienie jest odrzucane
```
