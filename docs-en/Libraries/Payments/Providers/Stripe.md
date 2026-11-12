# TailoredApps.Shared.Payments.Provider.Stripe

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Stripe)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Stripe/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **Stripe Checkout** — a global operator of card, BLIK (PLN) and Przelewy24 payments. The provider uses a Stripe Checkout Session (hosted page).

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Stripe
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.Stripe;

builder.Services
    .AddPayments()
    .RegisterStripeProvider();
```

---

## Configuration `appsettings.json`

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

| Option | Required | Description |
|-------|------|------|
| `SecretKey` | yes | Stripe API key (`sk_live_...` or `sk_test_...`) |
| `WebhookSecret` | yes | Webhook endpoint secret (`whsec_...`) |
| `SuccessUrl` | no | Return URL after a successful payment; Stripe replaces `{CHECKOUT_SESSION_ID}` with the session id |
| `CancelUrl` | no | Return URL after the payment is cancelled or fails |

Required options are checked when the host starts (`OptionsValidationException`); the others are not validated. There is no
test-mode option: test mode means using an `sk_test_...` key.

The payment methods of a session are set with a separate list, `Stripe:AllowedPaymentMethods` (at the top level of the configuration,
not under `Payments:Providers:Stripe`). When it is empty the provider uses `card` and `blik` for PLN, `card` and `sepa_debit` for EUR,
and `card` for other currencies.

---

## Supported channels

The list `GetChannels` returns is fixed in the provider:

| Currency | Channels |
|--------|--------|
| PLN | `card` (Visa, Mastercard, Amex), `blik`, `p24` (Przelewy24) |
| EUR | `card`, `sepa_debit` |
| Other | `card` |

The methods a session actually offers are the ones described under configuration: `p24` is not in the default PLN set, and
`PaymentRequest.PaymentChannel` does not select a method (it is stored in the session metadata).

---

## Payment flow

1. `RegisterPayment` → creates a Stripe Checkout Session
2. The user is redirected to `RedirectUrl` (hosted Stripe page)
3. After the payment Stripe calls the webhook with the `checkout.session.completed` / `payment_intent.payment_failed` events
4. The provider processes the webhook and returns `PaymentWebhookResult`

---

## Gateway errors

The Stripe.net SDK reports every API error as `StripeException`. From 3.0 the provider turns it into the outcomes every provider reports
(in 2.x `StripeException` reached the application):

| Situation | Outcome |
|---|---|
| `RegisterPayment`: Stripe could not handle the call (5xx, 408, 429, no answer) | `PaymentGatewayException` with the `StripeException` as `InnerException` - the session may or may not exist |
| `RegisterPayment`: a 4xx answer (invalid parameters, a payment method not enabled, a wrong key) | `PaymentStatus = Rejected`, Stripe's message in `ResponseObject` |
| `GetStatus`: any error, a 404 included | `PaymentGatewayException` - a failed query says nothing about the payment |

```csharp
try
{
    var payment = await payments.RegisterPayment(new PaymentRequest("Stripe", 129.00m, "PLN") { Title = "Order 42" }, cancellationToken);
    if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
        return UnprocessableEntity(payment.ResponseObject);   // Stripe refused
    return Redirect(payment.RedirectUrl!);
}
catch (HttpRequestException)   // PaymentGatewayException: the state of the payment is not known
{
    return StatusCode(StatusCodes.Status503ServiceUnavailable);
}
```

---

## Webhook

Stripe verifies the webhook through `Stripe-Signature` in the header. Access to the **raw body** of the HTTP request is required.

```csharp
[HttpPost("webhooks/stripe")]
public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
{
    // The signature covers the exact bytes of the body - read it raw, not through [FromBody].
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

| Event | Result |
|-------|--------|
| `checkout.session.completed` with `payment_status` = `paid`, `checkout.session.async_payment_succeeded` | `PaymentWebhookProcessed`, status `Finished`, session id |
| `checkout.session.async_payment_failed`, `checkout.session.expired` | `PaymentWebhookProcessed`, status `Rejected`, session id |
| `payment_intent.succeeded` / `payment_intent.payment_failed` | `PaymentWebhookProcessed`, status `Finished` / `Rejected`, payment intent id |
| `checkout.session.completed` not yet paid, another event, an event without an identifier | `PaymentWebhookIgnored` |

!!! warning "Raw body"
    Verifying the Stripe signature requires the raw request body. Do not use `[FromBody]` with automatic JSON deserialization — read `Request.Body` directly instead.

---

## 🔒 Security

- A missing `WebhookSecret` or `Stripe-Signature` header, or a signature that does not match, yields `PaymentWebhookRejected` (`InvalidSignature`) - Stripe.net is no longer invoked with an empty key.
- `checkout.session.expired`, `checkout.session.async_payment_succeeded/failed` and `payment_intent.succeeded/payment_failed` carry `PaymentUniqueId` (session or payment-intent id).
- The `payment_intent.*` events carry the payment intent id, not the session id returned by `RegisterPayment`. To match webhooks with stored session ids, act on the `checkout.session.*` events.

---

## 🤖 AI Agent Prompt

```markdown
## Stripe Provider - instructions for an AI agent

Provider key: "Stripe"

Configuration section: "Payments:Providers:Stripe"

Required fields: SecretKey, WebhookSecret

Optional: SuccessUrl, CancelUrl; payment methods in "Stripe:AllowedPaymentMethods" (top-level section)

Flow: RegisterPayment → Checkout Session → redirect → webhook

Webhook: requires the raw body (not [FromBody]) + the Stripe-Signature header

Sandbox: SecretKey = "sk_test_..." (there is no IsTest option)

Registration: builder.Services.AddPayments().RegisterStripeProvider();
- WebhookSecret is mandatory - the host does not start without it, and a webhook that fails verification is PaymentWebhookRejected
- payment_intent.* events carry the payment intent id, not the session id - match stored session ids on checkout.session.* events
- Wrap RegisterPayment and GetStatus in catch (HttpRequestException) - a Stripe outage is PaymentGatewayException; RegisterPayment returns a refusal (4xx) as PaymentStatus == Rejected. Do not catch StripeException around these calls
```
