# TailoredApps.Shared.Payments

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments)](https://www.nuget.org/packages/TailoredApps.Shared.Payments/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a unified abstraction for payment gateway integration in .NET applications. Instead of writing separate logic for each payment operator, you program against the common `IPaymentService` interface, which automatically routes requests to the correct provider based on its key (`"Stripe"`, `"PayU"`, `"CashBill"`, etc.).

Key architectural elements:

- **`IPaymentProvider`** — contract for each provider (channels, payment initiation, status, webhook)
- **`IWebhookPaymentProvider`** — extension for providers supporting webhooks with signature verification
- **`IPaymentService`** — facade aggregating all registered providers
- **`PaymentOptionsBuilder`** — fluent API for registering providers in DI

`TailoredApps.Shared.Payments` is the core — concrete provider implementations are in separate `TailoredApps.Shared.Payments.Provider.*` packages.

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

    public async Task<PaymentStatusEnum> CheckStatusAsync(string providerId, string paymentId)
    {
        var response = await _payments.GetStatus(providerId, paymentId);
        return response.PaymentStatus;
    }
}
```

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
| `IPaymentService` | Interface | Facade: `GetProviders`, `GetChannels`, `RegisterPayment`, `GetStatus`, `HandleWebhookAsync`. Every method takes an optional `CancellationToken` |
| `IPaymentProvider` | Interface | Provider contract: `Key`, `Name`, `GetPaymentChannels`, `RequestPayment`, `GetStatus`, `TransactionStatusChange`. Every asynchronous method takes an optional `CancellationToken` |
| `IWebhookPaymentProvider` | Interfejs | Rozszerzenie: `HandleWebhookAsync(PaymentWebhookRequest)` |
| `IPaymentOptionsBuilder` | Interfejs | Fluent builder: `RegisterPaymentProvider<T>()` |

### Models

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `PaymentRequest` | Klasa | Dane płatności: provider, kanał, kwota, waluta, dane płatnika |
| `PaymentResponse` | Klasa | Wynik: `RedirectUrl`, `PaymentUniqueId`, `PaymentStatus` |
| `PaymentStatusEnum` | Enum | `Created`, `Processing`, `Finished`, `Rejected` |
| `PaymentModel` | Enum | `OneTime`, `Subscription` |
| `PaymentChannel` | Klasa | Kanał płatności: `Id`, `Name`, `Description`, `PaymentModel` |
| `PaymentProvider` | Klasa | Metadane providera: `Key`, `Name`, `Description`, `Url` |
| `PaymentWebhookRequest` | Class | HTTP data of the webhook request: `Body`, `Headers`, `Query` |
| `PaymentWebhookResult` | Record (closed hierarchy) | Exactly one of: `PaymentWebhookProcessed` (`Payment`), `PaymentWebhookIgnored` (`Reason`, `Payment`), `PaymentWebhookRejected` (`Rejection`, `Message`). For compatibility: `Success`, `Ignored`, `PaymentResponse`, `ErrorMessage` |
| `PaymentWebhookRejection` | Enum | Why a webhook was rejected: `InvalidSignature`, `NotConfigured`, `MalformedPayload`, `ProcessingFailed`, `UnknownProvider`, `NotSupported`, `Unspecified` |

### Startup validation of configuration

Every `Register*Provider()` checks the settings the provider cannot work without: its API credentials and the secret that signs its webhooks. If one is missing the host **does not start** — the `OptionsValidationException` names every missing setting (for example `'Payments:Providers:PayU:SignatureKey' is not configured.`). Return and notification URLs are not validated.

| Provider (`Payments:Providers:<Name>`) | Required settings |
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

Register only the providers you actually use and have configured in the given environment.

### DI Registration

| Metoda | Opis |
|--------|------|
| `services.AddPayments()` | Rejestruje `IPaymentService`/`PaymentService` |
| `builder.RegisterPaymentProvider<T>()` | Dodaje implementację `IPaymentProvider` |

---

## 🔒 Webhook security

- **Fail-closed.** Every provider rejects a notification when its secret (HMAC key, `SignatureKey`, `CrcKey`, `SecurityCode`, `ShopSecretPhrase`, `WebhookSecret`) is not configured or the signature is empty. Previously an empty secret meant a signature computed with an empty key, which anyone could reproduce.
- **Constant-time comparison.** Signatures are compared with `WebhookSignature.FixedTimeEquals` / `FixedTimeEqualsIgnoreCase` (`TailoredApps.Shared.Payments.Security`), so response timing does not leak the expected value. Use these helpers in custom providers.
- **Case-insensitive headers and query.** `PaymentWebhookRequest.Headers`/`Query` resolve `stripe-signature` and `Stripe-Signature` alike - HTTP/2 lower-cases header names.
- **Identifiers in URLs.** `PaymentIdentifier.EnsureSafe` rejects identifiers containing `/`, `?`, `#`, whitespace etc. before they are placed in a gateway request URL (prevents re-targeting an authenticated request to another endpoint).
- **Error messages never disclose the signature.** `PaymentWebhookResult.ErrorMessage` carries a short description ("Invalid signature.") and never the expected value.
- **Webhook results identify the payment.** `PaymentResponse.PaymentUniqueId` is populated from the payload (orderId, paymentId, sessionId, merchantReference, order_id, transactionId) - de-duplicate events on it. Non-terminal events (`Processing`) are returned as `Ignore`.

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
var status = await _payments.GetStatus("Stripe", paymentId);
// status.PaymentStatus: Created/Processing/Finished/Rejected
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
- A webhook result is always one of three records: PaymentWebhookProcessed (act on Payment), PaymentWebhookIgnored (answer 2xx, do nothing), PaymentWebhookRejected (trust nothing in the request)
- Read the rejection reason from PaymentWebhookRejected.Rejection (e.g. InvalidSignature -> 401, MalformedPayload -> 400) - never parse the Message text
- Do not add a default branch to a switch over the result: the hierarchy is closed and the C# 15 compiler checks completeness
- Kwota Amount w walucie bazowej (nie w groszach — chyba że provider wymaga inaczej)
- Dla webhooków wymagany jest endpoint HTTP POST z raw body
- Never run a provider without its webhook secret configured - verification rejects every notification (fail-closed)
- In custom providers compare signatures with WebhookSignature.FixedTimeEquals and pass URL identifiers through PaymentIdentifier.EnsureSafe
- Do not echo the webhook ErrorMessage to the caller - log it server-side
- De-duplicate events on PaymentUniqueId; the webhook consumer must be idempotent
- Register only configured providers: missing credentials or a missing webhook secret stop the application at startup (OptionsValidationException)
- Pass a CancellationToken (HttpContext.RequestAborted in a controller) to every IPaymentService call - it reaches the gateway HTTP calls
- Custom provider: every asynchronous method takes CancellationToken cancellationToken = default and forwards it to HttpClient
```
