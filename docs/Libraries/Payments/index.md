# TailoredApps.Shared.Payments

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments)](https://www.nuget.org/packages/TailoredApps.Shared.Payments/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

!!! tip "Aktualizujesz z 1.x?"
    Zmiany wymagane w kodzie aplikacji są zebrane na stronie [Aktualizacja do 2.0](../../Migration/v2.md).

## Opis

Biblioteka dostarcza zunifikowaną abstrakcję do integracji z bramkami płatności w aplikacjach .NET. Zamiast pisać osobną logikę dla każdego operatora płatności, programujesz do wspólnego interfejsu `IPaymentService`, który automatycznie kieruje żądania do właściwego providera na podstawie klucza (`"Stripe"`, `"PayU"`, `"CashBill"` itd.).

Kluczowe elementy architektury:

- **`IPaymentProvider`** — kontrakt dla każdego providera (kanały, inicjowanie płatności, status, webhook)
- **`IWebhookPaymentProvider`** — rozszerzenie dla providerów obsługujących webhooks z weryfikacją podpisu
- **`IPaymentService`** — fasada agregująca wszystkich zarejestrowanych providerów
- **`PaymentOptionsBuilder`** — fluent API do rejestracji providerów w DI

Biblioteka `TailoredApps.Shared.Payments` to samo serce — konkretne implementacje providerów są w osobnych pakietach `TailoredApps.Shared.Payments.Provider.*`.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments

# Dodaj wybrane providery:
dotnet add package TailoredApps.Shared.Payments.Provider.Stripe
dotnet add package TailoredApps.Shared.Payments.Provider.PayU
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Stripe;
using TailoredApps.Shared.Payments.Provider.PayU;

builder.Services
    .AddPayments()
    .RegisterPaymentProvider<StripeProvider>()
    .RegisterPaymentProvider<PayUProvider>();
```

---

## Przykład użycia

### Inicjowanie płatności

```csharp
public class CheckoutService
{
    private readonly IPaymentService _payments;

    public CheckoutService(IPaymentService payments) => _payments = payments;

    public async Task<string> CreatePaymentAsync(CartDto cart, string email)
    {
        var response = await _payments.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "Stripe",       // lub "PayU", "CashBill" itp.
            PaymentChannel = "card",
            PaymentModel = PaymentModel.OneTime,
            Title = $"Zamówienie #{cart.OrderId}",
            Description = "Zakup w sklepie MyShop",
            Currency = "PLN",
            Amount = cart.TotalAmount,
            Email = email,
            FirstName = cart.CustomerFirstName,
            Surname = cart.CustomerLastName
        });

        // Przekieruj użytkownika na stronę płatności
        return response.RedirectUrl;
    }

    public async Task<PaymentStatusEnum?> CheckStatusAsync(string providerId, string paymentId)
    {
        try
        {
            var response = await _payments.GetStatus(providerId, paymentId);
            return response.PaymentStatus;
        }
        catch (HttpRequestException)   // awaria bramki (obejmuje PaymentGatewayException)
        {
            return null;               // stan nieznany: nie zmieniaj zamówienia, sprawdź później
        }
    }
}
```

`PaymentProvider`, `Amount` i `Currency` są składowymi `required`: inicjalizator obiektu, który pominie którąś z nich, nie skompiluje się
(CS9035), a `System.Text.Json` odrzuci dokument bez nich (`JsonException`, w kontrolerze API odpowiedź 400). Krótsza forma to konstruktor
`new PaymentRequest("Stripe", cart.TotalAmount, "PLN") { Title = "…" }`. `RegisterPayment` dodatkowo odrzuca pusty klucz providera lub
pustą walutę (`ArgumentException`) oraz kwotę mniejszą lub równą zero (`ArgumentOutOfRangeException`), zanim wywoła bramkę.

**Zmiana niekompatybilna (2.0):** kod tworzący `PaymentRequest` bez którejś z tych trzech składowych przestaje się kompilować.

### Obsługa webhooków

```csharp
[ApiController]
[Route("api/webhooks/payments")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentWebhookController(IPaymentService payments) => _payments = payments;

    [HttpPost("{providerKey}")]
    public async Task<IActionResult> HandleWebhook(
        string providerKey,
        [FromBody] JsonElement body)
    {
        var request = new PaymentWebhookRequest
        {
            Body = body.GetRawText(),
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
            Query = Request.Query.ToDictionary(q => q.Key, q => q.Value)
        };

        var result = await _payments.HandleWebhookAsync(providerKey, request, HttpContext.RequestAborted);

        return result switch
        {
            PaymentWebhookProcessed => Ok(),
            PaymentWebhookIgnored => NoContent(),
            PaymentWebhookRejected { Rejection: PaymentWebhookRejection.InvalidSignature } => Unauthorized(),
            PaymentWebhookRejected => BadRequest(),
        };
    }
}
```

### Pobieranie dostępnych kanałów płatności

```csharp
// Wyświetl dostępne metody płatności dla waluty PLN
var providers = await _payments.GetProviders();

foreach (var provider in providers)
{
    var channels = await _payments.GetChannels(provider.Key, "PLN");
    Console.WriteLine($"{provider.Name}: {string.Join(", ", channels.Select(c => c.Name))}");
}
```

---

## API Reference

### Core Interfaces

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IPaymentService` | Interfejs | Fasada: `GetProviders`, `GetChannels`, `RegisterPayment`, `GetStatus`, `HandleWebhookAsync`. Każda metoda przyjmuje opcjonalny `CancellationToken` |
| `IPaymentProvider` | Interfejs | Kontrakt providera: `Key`, `Name`, `GetPaymentChannels`, `RequestPayment`, `GetStatus`, `TransactionStatusChange`. Każda metoda asynchroniczna przyjmuje opcjonalny `CancellationToken` |
| `IWebhookPaymentProvider` | Interfejs | Rozszerzenie: `HandleWebhookAsync(PaymentWebhookRequest)` |
| `IPaymentOptionsBuilder` | Interfejs | Fluent builder: `RegisterPaymentProvider<T>()` |

### Models

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `PaymentRequest` | Klasa | Dane płatności. Wymagane (`required`): `PaymentProvider`, `Amount`, `Currency`; opcjonalne: kanał, tytuł, opis, dane płatnika, `AdditionalData`. Konstruktor `PaymentRequest(paymentProvider, amount, currency)` |
| `PaymentResponse` | Klasa | Wynik: `RedirectUrl`, `PaymentUniqueId`, `PaymentStatus` |
| `PaymentStatusEnum` | Enum | `Created`, `Processing`, `Finished`, `Rejected` |
| `PaymentModel` | Enum | `OneTime`, `Subscription` |
| `PaymentChannel` | Klasa | Kanał płatności: `Id`, `Name`, `Description`, `PaymentModel` |
| `PaymentProvider` | Klasa | Metadane providera: `Key`, `Name`, `Description`, `Url` |
| `PaymentWebhookRequest` | Klasa | Dane HTTP żądania webhook: `Body`, `Headers`, `Query` |
| `PaymentWebhookResult` | Rekord (zamknięta hierarchia) | Dokładnie jeden z: `PaymentWebhookProcessed` (`Payment`), `PaymentWebhookIgnored` (`Reason`, `Payment`), `PaymentWebhookRejected` (`Rejection`, `Message`). Zgodnościowo: `Success`, `Ignored`, `PaymentResponse`, `ErrorMessage` |
| `PaymentWebhookRejection` | Enum | Powód odrzucenia: `InvalidSignature`, `NotConfigured`, `MalformedPayload`, `ProcessingFailed`, `UnknownProvider`, `NotSupported`, `Unspecified` |
| `PaymentGatewayException` | Wyjątek (`HttpRequestException`) | Bramka nie dała użytecznej odpowiedzi; stan płatności nieznany. `Provider`, `StatusCode`, `IsTransient`, `ResponseBody` |

### Walidacja konfiguracji przy starcie

Każde `Register*Provider()` sprawdza ustawienia, bez których provider nie może działać: dane dostępowe do API oraz sekret podpisujący webhooki. Gdy czegoś brakuje, host **nie wystartuje** — `OptionsValidationException` wymienia każde brakujące ustawienie (np. `'Payments:Providers:PayU:SignatureKey' is not configured.`). Adresy powrotu i powiadomień nie są walidowane.

| Provider (`Payments:Providers:<Nazwa>`) | Wymagane ustawienia |
|---|---|
| Adyen | `ApiKey`, `MerchantAccount`, `NotificationHmacKey` |
| CashBill (`Payments:Providers:Cashbill`) | `ServiceUrl`, `ShopId`, `ShopSecretPhrase` |
| HotPay | `SecretHash` |
| PayNow | `ApiKey`, `SignatureKey` |
| PayU | `ClientId`, `ClientSecret`, `PosId`, `SignatureKey` |
| Przelewy24 | `MerchantId`, `PosId`, `ApiKey`, `CrcKey` |
| Revolut | `ApiKey`, `WebhookSecret` |
| Stripe | `SecretKey`, `WebhookSecret` |
| Tpay | `ClientId`, `ClientSecret`, `SecurityCode` |

Rejestruj tylko tych providerów, których faktycznie używasz i masz skonfigurowanych w danym środowisku.

### DI Registration

| Metoda | Opis |
|--------|------|
| `services.AddPayments()` | Rejestruje `IPaymentService`/`PaymentService` |
| `builder.RegisterPaymentProvider<T>()` | Dodaje implementację `IPaymentProvider` |

---

## ⚠️ Awaria bramki to nie odmowa

Status `Rejected` oznacza wyłącznie odpowiedź bramki: płatność odrzucona lub anulowana albo żądanie utworzenia płatności odrzucone (4xx).
Gdy bramka nie daje użytecznej odpowiedzi, provider rzuca **`PaymentGatewayException`** — stan płatności jest wtedy **nieznany**
i nie wolno na tej podstawie anulować zamówienia.

| Sytuacja | Wynik |
|---|---|
| Tworzenie płatności, bramka odpowiada 5xx, 408 lub 429 | `PaymentGatewayException` (`IsTransient == true`) |
| Tworzenie płatności, bramka odpowiada innym 4xx | `PaymentResponse` ze statusem `Rejected` i treścią błędu w `ResponseObject` |
| Zapytanie o status lub o token, odpowiedź spoza 2xx | `PaymentGatewayException` |
| Brak połączenia z bramką | `HttpRequestException` (klasa bazowa `PaymentGatewayException`) |
| Przekroczony czas | `TaskCanceledException` |

```csharp
try
{
    var payment = await _payments.GetStatus("Tpay", paymentId, cancellationToken);
    if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
        await _orders.CancelAsync(orderId, cancellationToken);   // bramka odpowiedziała: odrzucona
}
catch (HttpRequestException exception)   // obejmuje PaymentGatewayException
{
    // Stan płatności nieznany: zostaw zamówienie bez zmian i sprawdź ponownie później.
    _logger.LogWarning(exception, "Gateway unavailable for payment {PaymentId}", paymentId);
}
```

`PaymentGatewayException` niesie `Provider`, `StatusCode`, `IsTransient` oraz początek odpowiedzi bramki w `ResponseBody`
(treść nie trafia do `Message`, bo może zawierać dane żądania). Dotyczy providerów Adyen, CashBill, HotPay, PayNow, PayU, Przelewy24, Revolut i Tpay.
Stripe zgłasza awarie jako `StripeException`. CashBill do wersji 2.0.1 zgłaszał `WebException`; od kolejnej wersji także `PaymentGatewayException`.

Przelewy24: gdy w trakcie webhooka nie da się zweryfikować transakcji, wynikiem jest `PaymentWebhookRejected` (`ProcessingFailed`),
a nie przetworzona płatność odrzucona — odpowiedz kodem spoza 2xx, a Przelewy24 ponowi powiadomienie.

**Zmiana niekompatybilna (2.0):** do tej pory nieudane zapytanie o status zwracało `Rejected` (Revolut: `Created`), a błąd 5xx przy tworzeniu
płatności — `Rejected` albo `JsonException`.

---

## 🔒 Bezpieczeństwo webhooków

- **Fail-closed.** Każdy provider odrzuca powiadomienie, gdy sekret (klucz HMAC, `SignatureKey`, `CrcKey`, `SecurityCode`, `ShopSecretPhrase`, `WebhookSecret`) nie jest skonfigurowany lub podpis jest pusty. Wcześniej pusty sekret oznaczał podpis liczony z pustym kluczem, który mógł policzyć każdy.
- **Porównanie w stałym czasie.** Podpisy porównuje `WebhookSignature.FixedTimeEquals` / `FixedTimeEqualsIgnoreCase` (`TailoredApps.Shared.Payments.Security`), więc czas odpowiedzi nie zdradza oczekiwanej wartości. Użyj tych helperów we własnych providerach.
- **Nagłówki i query są case-insensitive.** `PaymentWebhookRequest.Headers`/`Query` znajdują `stripe-signature` i `Stripe-Signature` — HTTP/2 przesyła nazwy małymi literami.
- **Identyfikatory w URL.** `PaymentIdentifier.EnsureSafe` odrzuca identyfikatory z `/`, `?`, `#`, spacjami itp., zanim trafią do adresu żądania do bramki (ochrona przed przekierowaniem żądania z poświadczeniami sklepu na inny endpoint).
- **Komunikaty błędów nie ujawniają podpisu.** `PaymentWebhookResult.ErrorMessage` zawiera tylko krótki opis („Invalid signature.”), nigdy oczekiwaną wartość.
- **Wynik webhooka identyfikuje płatność.** `PaymentResponse.PaymentUniqueId` jest wypełniany z payloadu (orderId, paymentId, sessionId, merchantReference, order_id, transactionId) — na tej podstawie de-duplikuj zdarzenia. Zdarzenia niekońcowe (`Processing`) są zwracane jako `Ignore`.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Payments — Instrukcja dla agenta AI

Używasz TailoredApps.Shared.Payments do integracji z bramkami płatności.

### Rejestracja
```csharp
builder.Services.AddPayments()
    .RegisterPaymentProvider<StripeProvider>()
    .RegisterPaymentProvider<PayUProvider>();
```

### Inicjowanie płatności
```csharp
var response = await _payments.RegisterPayment(new PaymentRequest
{
    PaymentProvider = "Stripe",   // klucz providera
    PaymentChannel = "card",
    Currency = "PLN",
    Amount = 99.99m,
    Email = "user@example.com",
    Title = "Zamówienie #123"
});
string redirectUrl = response.RedirectUrl;  // → przeglądarka
```

### Sprawdzanie statusu
```csharp
try
{
    var status = await _payments.GetStatus("Stripe", paymentId);
    // status.PaymentStatus: Created/Processing/Finished/Rejected
}
catch (HttpRequestException)
{
    // awaria bramki: stan płatności nieznany — nie zmieniaj zamówienia, sprawdź później
}
```

### Obsługa webhooków
```csharp
var result = await _payments.HandleWebhookAsync(providerKey, new PaymentWebhookRequest
{
    Body = rawBody,
    Headers = httpHeaders,
    Query = query
});

var response = result switch
{
    PaymentWebhookProcessed processed => Handle(processed.Payment),   // PaymentUniqueId + PaymentStatus
    PaymentWebhookIgnored => NoContent(),                             // authentic, nothing to do
    PaymentWebhookRejected { Rejection: PaymentWebhookRejection.InvalidSignature } => Unauthorized(),
    PaymentWebhookRejected => BadRequest(),
};
```

### Zasady
- Każdy provider ma unikalny Key (np. "Stripe", "PayU", "CashBill")
- PaymentRequest: zawsze ustaw PaymentProvider, Amount (większe od zera) i Currency — są required; brak którejś to błąd kompilacji CS9035
- Łap HttpRequestException (obejmuje PaymentGatewayException) wokół RegisterPayment i GetStatus: awaria bramki oznacza stan nieznany — nie anuluj zamówienia, sprawdź ponownie później
- Status Rejected traktuj jako odmowę tylko wtedy, gdy wywołanie zakończyło się bez wyjątku
- Wynik webhooka to zawsze jeden z trzech rekordów: PaymentWebhookProcessed (działaj na Payment), PaymentWebhookIgnored (odpowiedz 2xx, nic nie rób), PaymentWebhookRejected (nie ufaj niczemu z żądania)
- Powód odrzucenia czytaj z PaymentWebhookRejected.Rejection (np. InvalidSignature → 401, MalformedPayload → 400) — nigdy nie analizuj treści Message
- W switch po wyniku nie dodawaj gałęzi domyślnej: hierarchia jest zamknięta i kompilator C# 15 pilnuje kompletności
- Kwota Amount w walucie bazowej (nie w groszach — chyba że provider wymaga inaczej)
- Dla webhooków wymagany jest endpoint HTTP POST z raw body
- Nigdy nie uruchamiaj providera bez skonfigurowanego sekretu webhooka — weryfikacja odrzuci wszystkie powiadomienia (fail-closed)
- We własnych providerach porównuj podpisy przez WebhookSignature.FixedTimeEquals, a identyfikatory do URL przepuszczaj przez PaymentIdentifier.EnsureSafe
- Nie zwracaj klientowi ErrorMessage z webhooka w treści odpowiedzi — loguj go po stronie serwera
- De-duplikuj zdarzenia po PaymentUniqueId; konsument webhooka musi być idempotentny
- Rejestruj tylko skonfigurowanych providerów: brak danych dostępowych lub sekretu webhooka zatrzymuje start aplikacji (OptionsValidationException)
- Przekazuj CancellationToken (w kontrolerze HttpContext.RequestAborted) do każdego wywołania IPaymentService — token dociera do wywołań HTTP bramki
- Własny provider: każda metoda asynchroniczna przyjmuje CancellationToken cancellationToken = default i przekazuje go dalej do HttpClient
```
