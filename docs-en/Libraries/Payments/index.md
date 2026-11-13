# TailoredApps.Shared.Payments

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments)](https://www.nuget.org/packages/TailoredApps.Shared.Payments/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

!!! tip "Upgrading from 1.x?"
    The changes required in application code are collected on the [Upgrading to 2.0](../../Migration/v2.md) page.

## Description

This library provides a unified abstraction for payment gateway integration in .NET applications. Instead of writing separate logic for each payment operator, you program against the common `IPaymentService` interface, which automatically routes requests to the correct provider based on its key (`"Stripe"`, `"PayU"`, `"CashBill"`, etc.).

Key architectural elements:

- **`IPaymentProvider`** — contract for each provider (channels, payment initiation, status, webhook)
- **`IWebhookPaymentProvider`** — extension for providers supporting webhooks with signature verification
- **`IPaymentService`** — facade aggregating all registered providers
- **`PaymentOptionsBuilder`** — fluent API for registering providers in DI

`TailoredApps.Shared.Payments` is the core — concrete provider implementations are in separate `TailoredApps.Shared.Payments.Provider.*` packages.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments

# Add the selected providers:
dotnet add package TailoredApps.Shared.Payments.Provider.Stripe
dotnet add package TailoredApps.Shared.Payments.Provider.PayU
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Stripe;
using TailoredApps.Shared.Payments.Provider.PayU;

builder.Services
    .AddPayments()
    .RegisterStripeProvider()
    .RegisterPayUProvider();
```

Every provider package has a `Register<Name>Provider()` method on the builder returned by `AddPayments()`: it registers the provider's
dependencies (options from configuration, the HTTP client, webhook handling) and adds the provider to `IPaymentService`. The calls chain.

`RegisterPaymentProvider<T>()` is for plugging in **your own** `IPaymentProvider` implementation. For the providers from the packages that
call alone is not enough - it does not register their dependencies. The former two-step form
(`builder.Services.RegisterStripeProvider();` plus `AddPayments().RegisterPaymentProvider<StripeProvider>()`) still works.

---

## Usage example

### Initiating a payment

```csharp
public class CheckoutService
{
    private readonly IPaymentService _payments;

    public CheckoutService(IPaymentService payments) => _payments = payments;

    public async Task<string> CreatePaymentAsync(CartDto cart, string email)
    {
        var response = await _payments.RegisterPayment(new PaymentRequest
        {
            PaymentProvider = "Stripe",       // or "PayU", "CashBill" etc.
            PaymentChannel = "card",
            PaymentModel = PaymentModel.OneTime,
            Title = $"Order #{cart.OrderId}",
            Description = "Purchase in the MyShop store",
            Currency = "PLN",
            Amount = cart.TotalAmount,
            Email = email,
            FirstName = cart.CustomerFirstName,
            Surname = cart.CustomerLastName
        });

        // Redirect the user to the payment page
        return response.RedirectUrl;
    }

    public async Task<PaymentStatusEnum?> CheckStatusAsync(string providerId, string paymentId)
    {
        try
        {
            var response = await _payments.GetStatus(providerId, paymentId);
            return response.PaymentStatus;
        }
        catch (HttpRequestException)   // a failing gateway (covers PaymentGatewayException)
        {
            return null;               // unknown state: leave the order alone, check again later
        }
    }
}
```

`PaymentProvider`, `Amount` and `Currency` are `required` members: an object initializer that leaves one out does not compile
(CS9035), and `System.Text.Json` refuses a document without them (`JsonException`, a 400 response in an API controller). The shorter form is
the constructor `new PaymentRequest("Stripe", cart.TotalAmount, "PLN") { Title = "…" }`. `RegisterPayment` also refuses a blank provider key
or currency (`ArgumentException`) and an amount of zero or less (`ArgumentOutOfRangeException`) before it calls the gateway.

**Breaking change (2.0):** code that creates a `PaymentRequest` without one of these three members no longer compiles.

### Handling webhooks

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

### Getting the available payment channels

```csharp
// Show the payment methods available for the PLN currency
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

| Type | Kind | Description |
|-----|--------|------|
| `IPaymentService` | Interface | Facade: `GetProviders`, `GetChannels`, `RegisterPayment`, `GetStatus`, `HandleWebhookAsync`. Every method takes an optional `CancellationToken` |
| `IPaymentProvider` | Interface | Provider contract: `Key`, `Name`, `GetPaymentChannels`, `RequestPayment`, `GetStatus`, `TransactionStatusChange`. Every asynchronous method takes an optional `CancellationToken` |
| `IWebhookPaymentProvider` | Interface | Extension: `HandleWebhookAsync(PaymentWebhookRequest, CancellationToken)` |
| `IPaymentOptionsBuilder` | Interface | Fluent builder: `RegisterPaymentProvider<T>()` |

### Models

| Type | Kind | Description |
|-----|--------|------|
| `PaymentRequest` | Class | Payment data. Required (`required`): `PaymentProvider`, `Amount`, `Currency`; optional: channel, title, description, payer details, `AdditionalData`. Constructor `PaymentRequest(paymentProvider, amount, currency)` |
| `PaymentResponse` | Class | Result: `RedirectUrl`, `PaymentUniqueId`, `PaymentStatus` |
| `PaymentStatusEnum` | Enum | `Created`, `Processing`, `Finished`, `Rejected` |
| `PaymentModel` | Enum | `OneTime`, `Subscription` |
| `PaymentChannel` | Class | Payment channel: `Id`, `Name`, `Description`, `PaymentModel` |
| `PaymentProvider` | Class | Provider metadata: `Key`, `Name`, `Description`, `Url` |
| `PaymentWebhookRequest` | Class | HTTP data of the webhook request: `Body`, `Headers`, `Query` |
| `PaymentWebhookResult` | Record (closed hierarchy) | Exactly one of: `PaymentWebhookProcessed` (`Payment`), `PaymentWebhookIgnored` (`Reason`, `Payment`), `PaymentWebhookRejected` (`Rejection`, `Message`). For compatibility: `Success`, `Ignored`, `PaymentResponse`, `ErrorMessage` |
| `PaymentWebhookRejection` | Enum | Why a webhook was rejected: `InvalidSignature`, `NotConfigured`, `MalformedPayload`, `ProcessingFailed`, `UnknownProvider`, `NotSupported`, `Unspecified` |
| `PaymentGatewayException` | Exception (`HttpRequestException`) | The gateway gave no usable answer; the state of the payment is not known. `Provider`, `StatusCode`, `IsTransient`, `ResponseBody` |

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

### Registration methods

| Method | Description |
|--------|------|
| `services.AddPayments()` | Registers `IPaymentService`/`PaymentService` |
| `builder.Register<Name>Provider()` | Registers a provider from a package (its dependencies + adding it to `IPaymentService`), e.g. `RegisterTpayProvider()`; returns the builder |
| `builder.RegisterPaymentProvider<T>()` | Adds your own `IPaymentProvider` implementation |

---

## ⚠️ A failing gateway is not a refusal

`Rejected` means only what the gateway said: the payment was declined or cancelled, or the request to create a payment was refused (4xx).
When the gateway gives no usable answer, the provider throws **`PaymentGatewayException`** - the state of the payment is then **not known**
and an order must not be cancelled on that basis.

| Situation | Outcome |
|---|---|
| Creating a payment, the gateway answers 5xx, 408 or 429 | `PaymentGatewayException` (`IsTransient == true`) |
| Creating a payment, the gateway answers another 4xx | `PaymentResponse` with status `Rejected` and the error text in `ResponseObject` |
| Status or token query, an answer outside 2xx | `PaymentGatewayException` |
| The gateway cannot be reached | `HttpRequestException` (the base class of `PaymentGatewayException`) |
| Timeout | `TaskCanceledException` |

```csharp
try
{
    var payment = await _payments.GetStatus("Tpay", paymentId, cancellationToken);
    if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
        await _orders.CancelAsync(orderId, cancellationToken);   // the gateway answered: declined
}
catch (HttpRequestException exception)   // covers PaymentGatewayException
{
    // The state of the payment is not known: leave the order as it is and check again later.
    _logger.LogWarning(exception, "Gateway unavailable for payment {PaymentId}", paymentId);
}
```

`PaymentGatewayException` carries `Provider`, `StatusCode`, `IsTransient` and the beginning of the gateway's answer in `ResponseBody`
(the text is kept out of `Message` because it may hold details of the request). It applies to all nine providers. From 3.0 Stripe turns the
SDK's `StripeException` into `PaymentGatewayException` (the original in `InnerException`) and returns a 4xx refusal as `Rejected`; in 2.x it let
`StripeException` through. CashBill threw `WebException` up to 2.0.1.

`Payments` and the HTTP providers (all but Stripe) are safe for trimming and NativeAOT - see [Trimming and NativeAOT](../../Platform/index.md#trimming-and-nativeaot).

Przelewy24: when a transaction cannot be verified during a webhook, the result is `PaymentWebhookRejected` (`ProcessingFailed`),
not a processed payment that was rejected - answer with a status outside 2xx and Przelewy24 sends the notification again.

**Breaking change (2.0):** until now a failed status query returned `Rejected` (Revolut: `Created`), and a 5xx while creating a payment
returned `Rejected` or threw `JsonException`.

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

````markdown
## TailoredApps.Shared.Payments - instructions for an AI agent

You use TailoredApps.Shared.Payments to integrate with payment gateways.

### Registration
```csharp
builder.Services.AddPayments()
    .RegisterStripeProvider()
    .RegisterPayUProvider();
```

### Initiating a payment
```csharp
var response = await _payments.RegisterPayment(new PaymentRequest
{
    PaymentProvider = "Stripe",   // provider key
    PaymentChannel = "card",
    Currency = "PLN",
    Amount = 99.99m,
    Email = "user@example.com",
    Title = "Order #123"
});
string redirectUrl = response.RedirectUrl;  // → browser
```

### Checking the status
```csharp
try
{
    var status = await _payments.GetStatus("Stripe", paymentId);
    // status.PaymentStatus: Created/Processing/Finished/Rejected
}
catch (HttpRequestException)
{
    // a failing gateway: the state of the payment is not known - leave the order alone, check again later
}
```

### Handling webhooks
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

### Rules
- Every provider has a unique Key (e.g. "Stripe", "PayU", "CashBill")
- Register a provider from a package with one call on the builder: AddPayments().Register<Name>Provider(); RegisterPaymentProvider<T>() is for your own implementations
- PaymentRequest: always set PaymentProvider, Amount (greater than zero) and Currency - they are required; leaving one out is compile error CS9035
- Catch HttpRequestException (covers PaymentGatewayException) around RegisterPayment and GetStatus: a failing gateway means an unknown state - do not cancel the order, check again later
- Treat Rejected as a refusal only when the call returned without an exception
- A webhook result is always one of three records: PaymentWebhookProcessed (act on Payment), PaymentWebhookIgnored (answer 2xx, do nothing), PaymentWebhookRejected (trust nothing in the request)
- Read the rejection reason from PaymentWebhookRejected.Rejection (e.g. InvalidSignature -> 401, MalformedPayload -> 400) - never parse the Message text
- Do not add a default branch to a switch over the result: the hierarchy is closed and the C# 15 compiler checks completeness
- The Amount is in the base currency (not in grosze — unless the provider requires otherwise)
- Webhooks require an HTTP POST endpoint with the raw body
- Never run a provider without its webhook secret configured - verification rejects every notification (fail-closed)
- In custom providers compare signatures with WebhookSignature.FixedTimeEquals and pass URL identifiers through PaymentIdentifier.EnsureSafe
- Do not echo the webhook ErrorMessage to the caller - log it server-side
- De-duplicate events on PaymentUniqueId; the webhook consumer must be idempotent
- Register only configured providers: missing credentials or a missing webhook secret stop the application at startup (OptionsValidationException)
- Pass a CancellationToken (HttpContext.RequestAborted in a controller) to every IPaymentService call - it reaches the gateway HTTP calls
- Custom provider: every asynchronous method takes CancellationToken cancellationToken = default and forwards it to HttpClient
````
