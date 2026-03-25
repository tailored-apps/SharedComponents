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
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            QueryParams = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString())
        };

        var result = await _payments.HandleWebhookAsync(providerKey, request);

        return result switch
        {
            PaymentWebhookResult.Success => Ok(),
            PaymentWebhookResult.Ignored => NoContent(),
            _ => BadRequest()
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
| `IPaymentService` | Interfejs | Fasada: `GetProviders`, `GetChannels`, `RegisterPayment`, `GetStatus`, `HandleWebhookAsync` |
| `IPaymentProvider` | Interfejs | Kontrakt providera: `Key`, `Name`, `GetPaymentChannels`, `RequestPayment`, `GetStatus`, `TransactionStatusChange` |
| `IWebhookPaymentProvider` | Interfejs | Rozszerzenie: `HandleWebhookAsync(PaymentWebhookRequest)` |
| `IPaymentOptionsBuilder` | Interfejs | Fluent builder: `RegisterPaymentProvider<T>()` |

### Models

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `PaymentRequest` | Klasa | Dane płatności: provider, kanał, kwota, waluta, dane płatnika |
| `PaymentResponse` | Klasa | Wynik: `RedirectUrl`, `PaymentUniqueId`, `PaymentStatus` |
| `PaymentStatusEnum` | Enum | `Created`, `Pending`, `Completed`, `Failed`, `Cancelled` |
| `PaymentModel` | Enum | `OneTime`, `Subscription` |
| `PaymentChannel` | Klasa | Kanał płatności: `Id`, `Name`, `Description`, `PaymentModel` |
| `PaymentProvider` | Klasa | Metadane providera: `Key`, `Name`, `Description`, `Url` |
| `PaymentWebhookRequest` | Klasa | Dane HTTP żądania webhook: `Body`, `Headers`, `QueryParams` |
| `PaymentWebhookResult` | Enum | `Success`, `Ignored`, `Fail` |

### DI Registration

| Metoda | Opis |
|--------|------|
| `services.AddPayments()` | Rejestruje `IPaymentService`/`PaymentService` |
| `builder.RegisterPaymentProvider<T>()` | Dodaje implementację `IPaymentProvider` |

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
// status.PaymentStatus: Created/Pending/Completed/Failed/Cancelled
```

### Obsługa webhooków
```csharp
var result = await _payments.HandleWebhookAsync(providerKey, new PaymentWebhookRequest
{
    Body = rawBody,
    Headers = httpHeaders,
    QueryParams = queryParams
});
```

### Zasady
- Każdy provider ma unikalny Key (np. "Stripe", "PayU", "CashBill")
- Zawsze sprawdź PaymentWebhookResult — Ignored to OK, Fail to błąd podpisu/konfiguracji
- Kwota Amount w walucie bazowej (nie w groszach — chyba że provider wymaga inaczej)
- Dla webhooków wymagany jest endpoint HTTP POST z raw body
```
