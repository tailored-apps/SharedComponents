# TailoredApps .NET Shared Components

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Witaj w dokumentacji **TailoredApps Shared Components** — zestawu wielokrotnego użytku bibliotek .NET, które przyspieszają tworzenie profesjonalnych aplikacji webowych. Każda biblioteka rozwiązuje jeden konkretny problem i jest zaprojektowana pod kątem testowalności, rozszerzalności i integracji z ekosystemem ASP.NET Core + MediatR.

---

## Biblioteki

| Biblioteka | NuGet | Opis |
|---|---|---|
| [DateTime](Libraries/DateTime/index.md) | `TailoredApps.Shared.DateTime` | Abstrakcja `IDateTimeProvider` do mockowania czasu w testach |
| [Email](Libraries/Email/index.md) | `TailoredApps.Shared.Email` | SMTP provider, builder szablonów emaili, tryb konsolowy |
| [Email.Models](Libraries/Email/Models.md) | `TailoredApps.Shared.Email.Models` | Model wiadomości `MailMessage` |
| [Email.Office365](Libraries/Email/Office365.md) | `TailoredApps.Shared.Email.Office365` | Wysyłanie przez Microsoft Graph API (IMAP OAuth2) |
| [EntityFramework](Libraries/EntityFramework/index.md) | `TailoredApps.Shared.EntityFramework` | UnitOfWork pattern na EF Core z auditingiem i hookami |
| [EntityFramework.UnitOfWork.WebApiCore](Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore` | Automatyczne transakcje przez ASP.NET Core filter |
| [ExceptionHandling](Libraries/ExceptionHandling/index.md) | `TailoredApps.Shared.ExceptionHandling` | Middleware i filter do obsługi wyjątków w Web API |
| [MediatR](Libraries/MediatR/index.md) | `TailoredApps.Shared.MediatR` | Pipeline behaviors: Logging, Validation, Caching, Fallback, Retry |
| [MediatR.Caching](Libraries/MediatR/Caching.md) | `TailoredApps.Shared.MediatR.Caching` | Marker interface `ICachableRequest` dla cachowania requestów |
| [MediatR.Email](Libraries/MediatR/Email.md) | `TailoredApps.Shared.MediatR.Email` | `SendMail` command + handler — email przez pipeline MediatR |
| [MediatR.ML](Libraries/MediatR/ML.md) | `TailoredApps.Shared.MediatR.ML` | Klasyfikacja obrazów przez ML.NET w pipelines MediatR |
| [MediatR.PagedRequest](Libraries/MediatR/PagedRequest.md) | `TailoredApps.Shared.MediatR.PagedRequest` | Bazowy request MediatR z paginacją i sortowaniem |
| [Payments](Libraries/Payments/index.md) | `TailoredApps.Shared.Payments` | Abstrakcja bramki płatności — IPaymentService + IPaymentProvider |
| [Payments.Provider.Adyen](Libraries/Payments/Providers/Adyen.md) | `TailoredApps.Shared.Payments.Provider.Adyen` | Integracja Adyen |
| [Payments.Provider.CashBill](Libraries/Payments/Providers/CashBill.md) | `TailoredApps.Shared.Payments.Provider.CashBill` | Integracja CashBill |
| [Payments.Provider.HotPay](Libraries/Payments/Providers/HotPay.md) | `TailoredApps.Shared.Payments.Provider.HotPay` | Integracja HotPay |
| [Payments.Provider.PayNow](Libraries/Payments/Providers/PayNow.md) | `TailoredApps.Shared.Payments.Provider.PayNow` | Integracja PayNow (mBank) |
| [Payments.Provider.PayU](Libraries/Payments/Providers/PayU.md) | `TailoredApps.Shared.Payments.Provider.PayU` | Integracja PayU |
| [Payments.Provider.Przelewy24](Libraries/Payments/Providers/Przelewy24.md) | `TailoredApps.Shared.Payments.Provider.Przelewy24` | Integracja Przelewy24 |
| [Payments.Provider.Revolut](Libraries/Payments/Providers/Revolut.md) | `TailoredApps.Shared.Payments.Provider.Revolut` | Integracja Revolut Pay |
| [Payments.Provider.Stripe](Libraries/Payments/Providers/Stripe.md) | `TailoredApps.Shared.Payments.Provider.Stripe` | Integracja Stripe Checkout |
| [Payments.Provider.Tpay](Libraries/Payments/Providers/Tpay.md) | `TailoredApps.Shared.Payments.Provider.Tpay` | Integracja Tpay |
| [Querying](Libraries/Querying/index.md) | `TailoredApps.Shared.Querying` | Bazowe klasy do zapytań: `QueryBase`, `PagedAndSortedQuery`, `IPagedResult` |

---

## Szybki start

```bash
# Zainstaluj wybraną bibliotekę
dotnet add package TailoredApps.Shared.MediatR
dotnet add package TailoredApps.Shared.EntityFramework
dotnet add package TailoredApps.Shared.Payments
```

Pełna dokumentacja każdej biblioteki — łącznie z przykładami kodu, rejestracja DI i gotowymi promptami dla agentów AI — dostępna jest w sekcji **Libraries** w menu bocznym.

---

## Contributing

Zanim dodasz nową bibliotekę, przeczytaj [zasady contributingu](contributing.md) i [DOCUMENTATION_RULE](https://github.com/tailored-apps/SharedComponents/blob/master/DOCUMENTATION_RULE.md).

**Każda nowa biblioteka musi posiadać stronę dokumentacji — PR bez niej zostanie odrzucony.**
