# TailoredApps .NET Shared Components

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Welcome to the documentation of **TailoredApps Shared Components** — a set of reusable .NET libraries that accelerate the development of professional web applications. Each library solves one specific problem and is designed for testability, extensibility, and integration with the ASP.NET Core + MediatR ecosystem.

---

## Libraries

| Library | NuGet | Description |
|---|---|---|
| [DateTime](Libraries/DateTime/index.md) | `TailoredApps.Shared.DateTime` | `IDateTimeProvider` abstraction for mocking time in tests |
| [Email](Libraries/Email/index.md) | `TailoredApps.Shared.Email` | SMTP provider, email template builder, console mode |
| [Email.Models](Libraries/Email/Models.md) | `TailoredApps.Shared.Email.Models` | `MailMessage` data model |
| [Email.Office365](Libraries/Email/Office365.md) | `TailoredApps.Shared.Email.Office365` | Sending via Microsoft Graph API (IMAP OAuth2) |
| [EntityFramework](Libraries/EntityFramework/index.md) | `TailoredApps.Shared.EntityFramework` | Unit of Work pattern on EF Core with auditing and hooks |
| [EntityFramework.UnitOfWork.WebApiCore](Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore` | Automatic transactions via ASP.NET Core filter |
| [ExceptionHandling](Libraries/ExceptionHandling/index.md) | `TailoredApps.Shared.ExceptionHandling` | Middleware and filter for exception handling in Web API |
| [MediatR](Libraries/MediatR/index.md) | `TailoredApps.Shared.MediatR` | Pipeline behaviors: Logging, Validation, Caching, Fallback, Retry |
| [MediatR.Caching](Libraries/MediatR/Caching.md) | `TailoredApps.Shared.MediatR.Caching` | `ICachableRequest` marker interface for caching requests |
| [MediatR.Email](Libraries/MediatR/Email.md) | `TailoredApps.Shared.MediatR.Email` | `SendMail` command + handler — email via MediatR pipeline |
| [MediatR.ML](Libraries/MediatR/ML.md) | `TailoredApps.Shared.MediatR.ML` | Image classification via ML.NET in MediatR pipelines |
| [MediatR.PagedRequest](Libraries/MediatR/PagedRequest.md) | `TailoredApps.Shared.MediatR.PagedRequest` | Base MediatR request with pagination and sorting |
| [Payments](Libraries/Payments/index.md) | `TailoredApps.Shared.Payments` | Payment gateway abstraction — IPaymentService + IPaymentProvider |
| [Payments.Provider.Adyen](Libraries/Payments/Providers/Adyen.md) | `TailoredApps.Shared.Payments.Provider.Adyen` | Adyen integration |
| [Payments.Provider.CashBill](Libraries/Payments/Providers/CashBill.md) | `TailoredApps.Shared.Payments.Provider.CashBill` | CashBill integration |
| [Payments.Provider.HotPay](Libraries/Payments/Providers/HotPay.md) | `TailoredApps.Shared.Payments.Provider.HotPay` | HotPay integration |
| [Payments.Provider.PayNow](Libraries/Payments/Providers/PayNow.md) | `TailoredApps.Shared.Payments.Provider.PayNow` | PayNow (mBank) integration |
| [Payments.Provider.PayU](Libraries/Payments/Providers/PayU.md) | `TailoredApps.Shared.Payments.Provider.PayU` | PayU integration |
| [Payments.Provider.Przelewy24](Libraries/Payments/Providers/Przelewy24.md) | `TailoredApps.Shared.Payments.Provider.Przelewy24` | Przelewy24 integration |
| [Payments.Provider.Revolut](Libraries/Payments/Providers/Revolut.md) | `TailoredApps.Shared.Payments.Provider.Revolut` | Revolut Pay integration |
| [Payments.Provider.Stripe](Libraries/Payments/Providers/Stripe.md) | `TailoredApps.Shared.Payments.Provider.Stripe` | Stripe Checkout integration |
| [Payments.Provider.Tpay](Libraries/Payments/Providers/Tpay.md) | `TailoredApps.Shared.Payments.Provider.Tpay` | Tpay integration |
| [Querying](Libraries/Querying/index.md) | `TailoredApps.Shared.Querying` | Base query classes: `QueryBase`, `PagedAndSortedQuery`, `IPagedResult` |

---

## Quick Start

```bash
# Install the chosen library
dotnet add package TailoredApps.Shared.MediatR
dotnet add package TailoredApps.Shared.EntityFramework
dotnet add package TailoredApps.Shared.Payments
```

Full documentation for each library — including code examples, DI registration, and ready-made AI agent prompts — is available in the **Libraries** section in the side menu.

---

## Contributing

Before adding a new library, read the [contributing guidelines](contributing.md) and [DOCUMENTATION_RULE](https://github.com/tailored-apps/SharedComponents/blob/master/DOCUMENTATION_RULE.md).

**Every new library must have a documentation page — PRs without one will be rejected.**
