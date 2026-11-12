# TailoredApps.Shared.Payments.Provider.Tpay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Tpay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Tpay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **Tpay** — polskim operatorem płatności online obsługującym szybkie przelewy, BLIK, karty i inne metody.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Tpay
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.Tpay;

builder.Services
    .AddPayments()
    .RegisterTpayProvider();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Tpay": {
        "ClientId": "your-client-id",
        "ClientSecret": "your-client-secret",
        "MerchantId": "your-merchant-id",
        "ServiceUrl": "https://api.tpay.com",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NotifyUrl": "https://myapp.com/webhooks/tpay",
        "SecurityCode": "your-security-code",
        "JwsCertificateBaseUrl": "https://secure.tpay.com"
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `ClientId` | Identyfikator klienta OAuth2 |
| `ClientSecret` | Sekret klienta OAuth2 |
| `MerchantId` | Identyfikator sprzedawcy |
| `ServiceUrl` | URL API Tpay (sandbox: `https://openapi.sandbox.tpay.com`) |
| `ReturnUrl` | URL powrotu po płatności |
| `NotifyUrl` | URL do powiadomień (webhook) |
| `SecurityCode` | Kod bezpieczeństwa (kod weryfikujący powiadomienia z panelu Tpay) — sekret pola `md5sum` |
| `JwsCertificateBaseUrl` | Adres, z którego Tpay udostępnia certyfikaty podpisu powiadomień. Domyślnie `https://secure.tpay.com`; sandbox: `https://secure.sandbox.tpay.com` |
| `JwsRootCertificatePem` | Opcjonalnie: treść `tpay-jws-root.pem` (PEM). Gdy ustawiona, zaufanie jest przypięte do niej i plik nie jest pobierany |

---

## Obsługiwane kanały

Tpay obsługuje PLN przez: szybkie przelewy bankowe, BLIK, karty, Google Pay, Apple Pay. Lista kanałów pobierana z API.

---

## Autoryzacja

Provider automatycznie pobiera token OAuth2 przez endpoint `/oauth/auth` i odświeża go po wygaśnięciu.

---

## Webhook

Tpay wysyła powiadomienie o transakcji metodą POST jako formularz `application/x-www-form-urlencoded`
(pola `id`, `tr_id`, `tr_amount`, `tr_crc`, `tr_status`, `md5sum`, …). Provider przyjmuje je dopiero po dwóch sprawdzeniach:

1. **`md5sum`** = `md5(id + tr_id + tr_amount + tr_crc + SecurityCode)` — dowodzi, że powiadomienie jest adresowane do tego sprzedawcy.
2. **Nagłówek `X-JWS-Signature`** — podpis RS256 dokładnych bajtów body. Certyfikat podpisujący jest pobierany z adresu `x5u`
   z nagłówka JWS (musi wskazywać na `JwsCertificateBaseUrl`) i musi łańcuchem prowadzić do certyfikatu głównego Tpay
   (`/x509/tpay-jws-root.pem`).

```csharp
[HttpPost("webhooks/tpay")]
public async Task<IActionResult> TpayWebhook(CancellationToken cancellationToken)
{
    // Podpis obejmuje dokładne bajty body - czytaj je surowe, nie przez [FromForm].
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("Tpay", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    // Tpay ponawia powiadomienie, dopóki nie dostanie odpowiedzi 200 z treścią "TRUE".
    return result is PaymentWebhookRejected
        ? BadRequest("FALSE")
        : Content(TpayProvider.Acknowledgement, "text/plain");
}
```

| `tr_status` | Wynik |
|-------------|-------|
| `TRUE`, `PAID` | `PaymentWebhookProcessed`, status `Finished` |
| `CHARGEBACK`, `FALSE` | `PaymentWebhookProcessed`, status `Rejected` |
| inna wartość / brak | `PaymentWebhookIgnored` (status `Processing`) |

`PaymentUniqueId` w wyniku to `tr_crc`, a gdy jest puste — `tr_id` (tytuł transakcji Tpay, `TR-…`). Identyfikator zwracany przez
`RequestPayment` **nie** występuje w powiadomieniu, dlatego przekaż własny numer zamówienia w `PaymentRequest.AdditionalData`:
provider wysyła go jako `hiddenDescription`, a Tpay odsyła w `tr_crc`.

---

## 🔒 Bezpieczeństwo

- Weryfikacja jest fail-closed: pusty `SecurityCode`, brak lub niezgodność `md5sum`, brak albo uszkodzony `X-JWS-Signature`,
  algorytm inny niż `RS256`, certyfikat spoza łańcucha Tpay lub po terminie ważności → `PaymentWebhookRejection.InvalidSignature`.
- `md5sum` jest sprawdzane **przed** pobraniem jakiegokolwiek certyfikatu, więc żądanie bez znajomości `SecurityCode` nie wywołuje ruchu wychodzącego.
- Adres `x5u` musi mieć ten sam schemat, host i port co `JwsCertificateBaseUrl` (porównywany jest cały origin, nie prefiks tekstu —
  `https://secure.tpay.com.example.net` jest odrzucany).
- `tpay-jws-root.pem` to paczka: CA wystawiające certyfikat podpisu oraz samopodpisany root. Kotwicą zaufania jest wyłącznie
  certyfikat samopodpisany; magazyn certyfikatów systemu nie jest używany. `JwsRootCertificatePem` pozwala przypiąć tę paczkę w konfiguracji.
- Certyfikaty są przechowywane w pamięci przez godzinę (`ITpayServiceCaller` jest singletonem). Gdy podpis nie zgadza się z certyfikatem
  z pamięci, certyfikaty są pobierane ponownie jeden raz — wymiana certyfikatu po stronie Tpay nie przerywa obsługi.
- Sandbox podpisuje innym CA: ustaw `JwsCertificateBaseUrl` na `https://secure.sandbox.tpay.com`.
- `JwsCertificateBaseUrl` (bezwzględny adres https) i `JwsRootCertificatePem` są walidowane przy starcie hosta.
- **Zmiana niekompatybilna (2.0):** `ITpayServiceCaller.VerifyNotification(body, signature)` zastąpiono przez
  `VerifyNotificationAsync(body, jwsSignature, cancellationToken)`; nagłówek `X-Signature` nie jest już czytany.
- Zakres weryfikacji: łańcuch sprawdzono na certyfikatach publikowanych przez Tpay (produkcja i sandbox), a podpis JWS i `md5sum` —
  testami z własnym łańcuchem certyfikatów. Przed wdrożeniem wykonaj płatność testową w sandboxie Tpay.

---

## 🤖 AI Agent Prompt

```markdown
## Tpay Provider — Instrukcja dla agenta AI

Provider key: "Tpay"

Sekcja konfiguracji: "Payments:Providers:Tpay"

Wymagane pola: ClientId, ClientSecret, MerchantId, ReturnUrl, NotifyUrl, SecurityCode

Autoryzacja: OAuth2 — automatyczna, token cache'owany

Sandbox: ServiceUrl = "https://openapi.sandbox.tpay.com"

Rejestracja: builder.Services.AddPayments().RegisterTpayProvider();

Webhook: POST application/x-www-form-urlencoded; weryfikacja md5sum + nagłówek X-JWS-Signature (RS256, łańcuch do roota Tpay)

Zasady:
- Przekaż do PaymentWebhookRequest.Body surowe body (nie [FromForm]) i wszystkie nagłówki żądania
- Odpowiedz HTTP 200 z treścią "TRUE" (TpayProvider.Acknowledgement); inaczej Tpay ponowi powiadomienie
- W PaymentRequest.AdditionalData podaj własny numer zamówienia — wróci jako PaymentUniqueId w wyniku webhooka (tr_crc)
- Sandbox: JwsCertificateBaseUrl = "https://secure.sandbox.tpay.com" oraz ServiceUrl sandboxa
- Nie wyłączaj weryfikacji ani nie zastępuj ITpayServiceCaller stubem zwracającym true
```
