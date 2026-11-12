# TailoredApps.Shared.Payments

One interface, `IPaymentService`, in front of several payment gateways. A provider package per gateway
(`TailoredApps.Shared.Payments.Provider.*`: Adyen, CashBill, HotPay, PayNow, PayU, Przelewy24, Revolut, Stripe, Tpay)
plugs into it; the application starts payments, reads their status and handles webhooks the same way for all of them.

## Usage

```csharp
// Program.cs
builder.Services
    .AddPayments()
    .RegisterTpayProvider();
```

```csharp
// PaymentProvider, Amount and Currency are required members.
var response = await payments.RegisterPayment(new PaymentRequest("Tpay", 49.99m, "PLN")
{
    Title = "Order 7",
    Email = "buyer@example.com",
    AdditionalData = "order-7",   // your own reference; it identifies the payment in Tpay and Adyen webhooks
}, cancellationToken);

return Redirect(response.RedirectUrl);
```

## Three things an application has to handle

**1. A failing gateway is an exception, not a rejected payment.**
`RegisterPayment` and `GetStatus` return `PaymentStatusEnum.Rejected` only when the gateway said so. When the gateway is failing,
refuses a status query or cannot be reached, they throw `PaymentGatewayException` (derives from `HttpRequestException`): the state
of the payment is not known and an order must not be cancelled on that basis.

```csharp
try
{
    var payment = await payments.GetStatus("Tpay", paymentId, cancellationToken);
    // act on payment.PaymentStatus
}
catch (HttpRequestException exception)   // covers PaymentGatewayException
{
    // unknown state: leave the order as it is and check again later
}
```

Stripe reports such failures with `StripeException`.

**2. `PaymentRequest` requires `PaymentProvider`, `Amount` and `Currency`.**
An object initializer that leaves one out does not compile (CS9035), and System.Text.Json refuses a document without them.

**3. Webhooks need the raw request body.**
Gateways sign the exact bytes they send. Read the body from the request stream (not through `[FromForm]` or `[FromBody]`), pass it with
every header to `HandleWebhookAsync`, and act on the result: `PaymentWebhookProcessed`, `PaymentWebhookIgnored` or
`PaymentWebhookRejected`. Tpay and Adyen expect a specific answer body (`TpayProvider.Acknowledgement`, `AdyenProvider.Acknowledgement`)
and identify the payment by the reference you sent in `PaymentRequest.AdditionalData`.

## Upgrading from 1.x

The three points above are the changes that affect application code. The full guide, with before/after examples:
https://shared.tailoredapps.pl/en/Migration/v2/

## Documentation

- Library: https://shared.tailoredapps.pl/en/Libraries/Payments/
- Providers, configuration keys and webhook details: the *Providers* pages under the same address
