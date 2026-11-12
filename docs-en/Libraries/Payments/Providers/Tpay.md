# TailoredApps.Shared.Payments.Provider.Tpay

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.Tpay)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.Tpay/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integration with **Tpay** — a Polish online payment operator that handles fast transfers, BLIK, cards and other methods.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.Tpay
```

---

## DI registration

```csharp
using TailoredApps.Shared.Payments.Provider.Tpay;

builder.Services
    .AddPayments()
    .RegisterTpayProvider();
```

---

## Configuration `appsettings.json`

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

| Option | Required | Description |
|-------|------|------|
| `ClientId` | yes | OAuth2 client identifier |
| `ClientSecret` | yes | OAuth2 client secret |
| `MerchantId` | no | Merchant identifier - bound from configuration, but not used by the provider |
| `ServiceUrl` | no | Tpay API URL, default `https://api.tpay.com` (sandbox: `https://openapi.sandbox.tpay.com`). The old alias `ApiUrl` was removed in 3.0: a configuration that still sets it stops the host from starting instead of silently going to production |
| `ReturnUrl` | no | Return URL after payment (used for both the success and the error case) |
| `NotifyUrl` | no | URL for notifications (webhook) |
| `SecurityCode` | yes | Security code (the notification verification code from the Tpay panel) - the secret of the `md5sum` field |
| `JwsCertificateBaseUrl` | no | The origin Tpay serves its notification signing certificates from. Default `https://secure.tpay.com`; sandbox: `https://secure.sandbox.tpay.com`. Must be an absolute https address |
| `JwsRootCertificatePem` | no | Optional: the content of `tpay-jws-root.pem` (PEM). When set, trust is pinned to it and the file is not downloaded |

Required options are checked when the host starts (`OptionsValidationException`), together with the format of
`JwsCertificateBaseUrl` and `JwsRootCertificatePem`; the others are not validated.

---

## Supported channels

The provider returns a fixed list for every currency: `blik` (BLIK), `card` (payment card), `bank` (online transfer).

---

## Authorization

The provider obtains an OAuth2 token through the `/oauth/auth` endpoint for every `RegisterPayment` and `GetStatus` call. The token is not cached.

---

## Webhook

Tpay sends a transaction notification as a POST with an `application/x-www-form-urlencoded` body
(fields `id`, `tr_id`, `tr_amount`, `tr_crc`, `tr_status`, `md5sum`, …). The provider accepts it only after two checks:

1. **`md5sum`** = `md5(id + tr_id + tr_amount + tr_crc + SecurityCode)` - proves the notification is addressed to this merchant.
2. **The `X-JWS-Signature` header** - an RS256 signature over the exact bytes of the body. The signing certificate is downloaded from the
   `x5u` address in the JWS header (it must point at `JwsCertificateBaseUrl`) and must chain to the Tpay root certificate
   (`/x509/tpay-jws-root.pem`).

```csharp
[HttpPost("webhooks/tpay")]
public async Task<IActionResult> TpayWebhook(CancellationToken cancellationToken)
{
    // The signature covers the exact bytes of the body - read it raw, not through [FromForm].
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var result = await _payments.HandleWebhookAsync("Tpay", new PaymentWebhookRequest
    {
        Body = body,
        ContentType = Request.ContentType,
        Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
    }, cancellationToken);

    // Tpay repeats the notification until it gets a 200 response with the body "TRUE".
    return result is PaymentWebhookRejected
        ? BadRequest("FALSE")
        : Content(TpayProvider.Acknowledgement, "text/plain");
}
```

| `tr_status` | Result |
|-------------|--------|
| `TRUE`, `PAID` | `PaymentWebhookProcessed`, status `Finished` |
| `CHARGEBACK`, `FALSE` | `PaymentWebhookProcessed`, status `Rejected` |
| anything else / absent | `PaymentWebhookIgnored` (status `Processing`) |

`PaymentUniqueId` in the result is `tr_crc`, or `tr_id` (the Tpay transaction title, `TR-…`) when `tr_crc` is empty. The identifier returned by
`RequestPayment` is **not** part of the notification, so pass your own order number in `PaymentRequest.AdditionalData`:
the provider sends it as `hiddenDescription` and Tpay returns it in `tr_crc`.

---

## 🔒 Security

- Verification fails closed: an empty `SecurityCode`, a missing or wrong `md5sum`, a missing or malformed `X-JWS-Signature`,
  an algorithm other than `RS256`, a certificate outside the Tpay chain or past its validity → `PaymentWebhookRejection.InvalidSignature`.
- `md5sum` is checked **before** any certificate is downloaded, so a request made without the `SecurityCode` causes no outgoing traffic.
- The `x5u` address must have the same scheme, host and port as `JwsCertificateBaseUrl` (the whole origin is compared, not a text prefix -
  `https://secure.tpay.com.example.net` is refused).
- `tpay-jws-root.pem` is a bundle: the CA that issues the signing certificate and the self-signed root. Only the self-signed certificate is a
  trust anchor; the machine certificate store is not used. `JwsRootCertificatePem` pins that bundle in configuration.
- Certificates are kept in memory for an hour (`ITpayServiceCaller` is a singleton). When a signature does not match a cached certificate,
  the certificates are fetched again once - a certificate replaced by Tpay does not interrupt processing.
- The sandbox signs with a different CA: set `JwsCertificateBaseUrl` to `https://secure.sandbox.tpay.com`.
- `JwsCertificateBaseUrl` (an absolute https address) and `JwsRootCertificatePem` are validated when the host starts.
- **Breaking change (2.0):** `ITpayServiceCaller.VerifyNotification(body, signature)` was replaced by
  `VerifyNotificationAsync(body, jwsSignature, cancellationToken)`; the `X-Signature` header is no longer read.
- What was verified: the chain against the certificates Tpay publishes (production and sandbox), the JWS signature and `md5sum` with tests
  that use their own certificate chain. Make a test payment in the Tpay sandbox before going live.

---

## 🤖 AI Agent Prompt

```markdown
## Tpay Provider - instructions for an AI agent

Provider key: "Tpay"

Configuration section: "Payments:Providers:Tpay"

Required fields: ClientId, ClientSecret, SecurityCode

Authorization: OAuth2 — automatic, a token is requested for every call (not cached)

Sandbox: ServiceUrl = "https://openapi.sandbox.tpay.com"

Registration: builder.Services.AddPayments().RegisterTpayProvider();

Webhook: POST application/x-www-form-urlencoded; verified with md5sum + the X-JWS-Signature header (RS256, chain to the Tpay root)

Rules:
- Pass the raw body (not [FromForm]) and every request header in PaymentWebhookRequest
- Answer HTTP 200 with the body "TRUE" (TpayProvider.Acknowledgement); otherwise Tpay repeats the notification
- Put your own order number in PaymentRequest.AdditionalData - it comes back as PaymentUniqueId in the webhook result (tr_crc)
- Sandbox: JwsCertificateBaseUrl = "https://secure.sandbox.tpay.com" plus the sandbox ServiceUrl
- Do not disable verification or replace ITpayServiceCaller with a stub returning true
```
