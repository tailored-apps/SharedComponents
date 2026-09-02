# TailoredApps.Shared.Payments.Provider.CashBill

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Payments.Provider.CashBill)](https://www.nuget.org/packages/TailoredApps.Shared.Payments.Provider.CashBill/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

Integracja z **CashBill** — polskim operatorem płatności online obsługującym przelewy, karty i BLIK.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Payments.Provider.CashBill
```

---

## Rejestracja w DI

```csharp
using TailoredApps.Shared.Payments.Provider.CashBill;

builder.Services
    .RegisterCashbillProvider();

builder.Services
    .AddPayments()
    .RegisterPaymentProvider<CashBillProvider>();
```

---

## Konfiguracja `appsettings.json`

```json
{
  "Payments": {
    "Providers": {
      "Cashbill": {
        "ShopId": "MY_SHOP_ID",
        "ShopSecretPhrase": "my_secret_phrase",
        "ServiceUrl": "https://pay.cashbill.eu",
        "ReturnUrl": "https://myapp.com/payment/return",
        "NegativeReturnUrl": "https://myapp.com/payment/failed"
      }
    }
  }
}
```

| Opcja | Opis |
|-------|------|
| `ShopId` | Identyfikator sklepu w CashBill |
| `ShopSecretPhrase` | Fraza sekretna do podpisywania żądań |
| `ServiceUrl` | URL API CashBill (domyślnie: `https://pay.cashbill.eu`) |
| `ReturnUrl` | URL powrotu po udanej płatności |
| `NegativeReturnUrl` | URL powrotu po nieudanej/anulowanej płatności |

---

## Obsługiwane kanały

CashBill zwraca dynamiczną listę kanałów pobraną z API dla danej waluty (PLN, EUR, USD itd.). Kanały obejmują przelew bankowy, karty, BLIK, e-portfele.

---

## Webhook

CashBill wysyła powiadomienia GET/POST na `NotifyUrl`. Podpis weryfikowany przez MD5 + sekret.

---

## 🔒 Security

- The notification signature is `MD5(cmd + args + ShopSecretPhrase)`, compared in constant time. Both `HandleWebhookAsync` and the legacy `TransactionStatusChange` **verify** it - an invalid signature yields `Fail("Invalid signature.")` / `Rejected` without polling the CashBill API.
- The error message no longer contains the expected signature (it used to be an oracle for forging arbitrary notifications).
- Missing `ShopSecretPhrase` → `Fail("Signature verification is not configured.")`.
- `GetStatus(paymentId)` rejects identifiers with special characters (`PaymentIdentifier.EnsureSafe`).

---

## 🤖 AI Agent Prompt

```markdown
## CashBill Provider — Instrukcja dla agenta AI

Provider key: "Cashbill"

Sekcja konfiguracji: "Payments:Providers:Cashbill" (małe "b" w Cashbill!)

Wymagane pola: ShopId, ShopSecretPhrase, ServiceUrl, ReturnUrl, NegativeReturnUrl

Rejestracja: builder.Services.RegisterCashbillProvider(); builder.Services.AddPayments().RegisterPaymentProvider<CashBillProvider>();
- Never return the webhook ErrorMessage to the caller; log it server-side
- Keep the shop secret in user-secrets/environment variables - never in the repository
```
