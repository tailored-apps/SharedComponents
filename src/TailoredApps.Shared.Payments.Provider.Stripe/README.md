# TailoredApps.Shared.Payments.Provider.Stripe

Stripe is a global payment operator. This package plugs Stripe Checkout into `TailoredApps.Shared.Payments` through the
Stripe.net SDK: it creates a Checkout Session and returns the address of Stripe's hosted payment page, reads the status of a
session, and verifies and handles Stripe webhooks.

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Stripe   # brings TailoredApps.Shared.Payments with it
```

## Registration

```csharp
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Stripe;

builder.Services.AddPayments().RegisterStripeProvider();
```

The call registers the provider with its dependencies and adds it to `IPaymentService`. Several providers chain:
`.AddPayments().RegisterStripeProvider().RegisterTpayProvider()`.

## Configuration

Options are bound from the `Payments:Providers:Stripe` section (`StripeServiceOptions`).

```json
{
  "Payments": {
    "Providers": {
      "Stripe": {
        "SecretKey": "<stripe-secret-key>",
        "WebhookSecret": "<webhook-signing-secret>",
        "SuccessUrl": "https://shop.example/payment/success?session={CHECKOUT_SESSION_ID}",
        "CancelUrl": "https://shop.example/payment/cancelled"
      }
    }
  }
}
```

| Key | Required | Meaning | Default |
| --- | --- | --- | --- |
| `SecretKey` | yes | Stripe secret key; passed with every SDK call | - |
| `WebhookSecret` | yes | Signing secret of the webhook endpoint | - |
| `SuccessUrl` | no | Return address after a payment; Stripe replaces `{CHECKOUT_SESSION_ID}` with the session id | empty |
| `CancelUrl` | no | Return address after the payer cancels | empty |

A required key that is missing stops the host at startup with `OptionsValidationException`. The other keys are not validated.

The payment methods of a session can be set with a separate list at `Stripe:AllowedPaymentMethods` (top level, not under
`Payments:Providers`). Without it the provider uses `card` and `blik` for PLN, `card` and `sepa_debit` for EUR, `card` otherwise.

## Usage

```csharp
// payments is an injected IPaymentService
var response = await payments.RegisterPayment(new PaymentRequest("Stripe", 49.99m, "PLN")
{
    Title = "Order 7",              // sent as the product name of the session's line item
    Email = "buyer@example.com",
    AdditionalData = "order-7",     // stored in the session metadata as additional_data
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

`response.PaymentUniqueId` is the Checkout Session id; pass it to `GetStatus("Stripe", sessionId)`.

## Webhooks

Stripe posts a JSON event and signs the raw body in the `Stripe-Signature` header. The provider verifies it with Stripe.net
(`EventUtility.ConstructEvent`) and `WebhookSecret`; an event whose API version differs from the SDK's is still accepted.

```csharp
using var reader = new StreamReader(Request.Body, Encoding.UTF8);
var result = await payments.HandleWebhookAsync("Stripe", new PaymentWebhookRequest
{
    Body = await reader.ReadToEndAsync(cancellationToken),   // raw body, not [FromBody]
    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
}, cancellationToken);
return result is PaymentWebhookRejected ? BadRequest() : Ok();
```

| Event | Result |
| --- | --- |
| `checkout.session.completed` with `payment_status` `paid`, `checkout.session.async_payment_succeeded` | `PaymentWebhookProcessed`, `Finished`, session id |
| `checkout.session.async_payment_failed`, `checkout.session.expired` | `PaymentWebhookProcessed`, `Rejected`, session id |
| `payment_intent.succeeded` / `payment_intent.payment_failed` | `PaymentWebhookProcessed`, `Finished` / `Rejected`, payment intent id |
| `checkout.session.completed` not yet paid, any other event, an event without an id | `PaymentWebhookIgnored` |
| signature missing or wrong, or `WebhookSecret` not configured | `PaymentWebhookRejected` (`PaymentWebhookRejection.InvalidSignature`) |

## Good to know

- Failures of the Stripe API surface as `StripeException` from `RegisterPayment` and `GetStatus`, not as
  `PaymentGatewayException`. `RegisterPayment` never returns `Rejected`; treat the exception as an unknown state.
- `payment_intent.*` events carry the payment intent id in `Payment.PaymentUniqueId`, not the session id `RegisterPayment`
  returned. To match webhooks with stored session ids, act on the `checkout.session.*` events.
- `GetStatus` maps a `complete` session that is `paid` to `Finished`, a `complete` session that is not paid to `Processing`,
  `expired` to `Rejected`, anything else to `Created`.
- The amount is sent in the minor unit (amount x 100), except for Stripe's zero-decimal currencies such as JPY.
- `GetChannels` returns a fixed list per currency (PLN: `card`, `blik`, `p24`; EUR: `card`, `sepa_debit`; other: `card`).
  `PaymentRequest.PaymentChannel` does not select a method; it is stored in the session metadata as `payment_channel`.

## Documentation

- Provider: https://shared.tailoredapps.pl/en/Libraries/Payments/Providers/Stripe/
- Core library: https://shared.tailoredapps.pl/en/Libraries/Payments/
