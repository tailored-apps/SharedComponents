# TailoredApps .NET Shared Components

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Witaj w dokumentacji **TailoredApps Shared Components** — zestawu wielokrotnego użytku bibliotek .NET, które przyspieszają tworzenie profesjonalnych aplikacji webowych. Każda biblioteka rozwiązuje jeden konkretny problem i jest zaprojektowana pod kątem testowalności, rozszerzalności i integracji z ekosystemem ASP.NET Core + mediator.

---

## Biblioteki

| Biblioteka | NuGet | Opis |
|---|---|---|
| [DateTime](Libraries/DateTime/index.md) | `TailoredApps.Shared.DateTime` | Abstrakcja `IDateTimeProvider` do mockowania czasu w testach (`DateTime`, `DateTimeOffset`) na `System.TimeProvider` |
| [Email](Libraries/Email/index.md) | `TailoredApps.Shared.Email` | SMTP provider, builder szablonów emaili, tryb konsolowy |
| [Email.Models](Libraries/Email/Models.md) | `TailoredApps.Shared.Email.Models` | Model wiadomości `MailMessage` |
| [Email.Office365](Libraries/Email/Office365.md) | `TailoredApps.Shared.Email.Office365` | Wysyłanie przez Microsoft Graph API (IMAP OAuth2) |
| [EntityFramework](Libraries/EntityFramework/index.md) | `TailoredApps.Shared.EntityFramework` | UnitOfWork pattern na EF Core z auditingiem i hookami |
| [EntityFramework.UnitOfWork.WebApiCore](Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore` | Automatyczne transakcje przez ASP.NET Core filter |
| [EntityFramework.UnitOfWork.MinimalApi](Libraries/EntityFramework/UnitOfWork.MinimalApi.md) | `TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi` | Automatyczne transakcje dla minimal API przez filtr endpointu |
| [ExceptionHandling](Libraries/ExceptionHandling/index.md) | `TailoredApps.Shared.ExceptionHandling` | Middleware i filter do obsługi wyjątków w Web API |
| [ExceptionHandling.MinimalApi](Libraries/ExceptionHandling/MinimalApi.md) | `TailoredApps.Shared.ExceptionHandling.MinimalApi` | Obsługa wyjątków i walidacja FluentValidation dla minimal API (filtry endpointów), zgodna z NativeAOT |
| [Mediator](Libraries/Mediator/index.md) | `TailoredApps.Shared.Mediator` | Pipeline behaviors: Logging, Validation, Caching, Fallback, Retry |
| [Mediator.Caching](Libraries/Mediator/Caching.md) | `TailoredApps.Shared.Mediator.Caching` | Marker interface `ICachableRequest` dla cachowania requestów |
| [Mediator.Email](Libraries/Mediator/Email.md) | `TailoredApps.Shared.Mediator.Email` | `SendMail` command + handler — email przez pipeline'em mediatora |
| [Mediator.ML](Libraries/Mediator/ML.md) | `TailoredApps.Shared.Mediator.ML` | Klasyfikacja obrazów przez ML.NET w pipeline'ach mediatora |
| [Mediator.PagedRequest](Libraries/Mediator/PagedRequest.md) | `TailoredApps.Shared.Mediator.PagedRequest` | Bazowy request mediatora z paginacją i sortowaniem |
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
| [WebApi](Libraries/WebApi/index.md) | `TailoredApps.Shared.WebApi` | Produkcyjne defaulty Web API: OpenTelemetry (metryki/tracing/logi przez OTLP), health checks, resilience HTTP |

---

## Aplikacja demo

Wszystkie biblioteki razem, w działającej aplikacji: [Przykładowy serwis i testy integracyjne](Demo/index.md) — sklep w Web API
(`samples/`: kontrakt, logika i host, z ForkForward.Mediator i ForkForward.Mapper) z podstawionymi bramkami płatności, uruchamiany bez kont i bez sieci, oraz testy, które sprawdzają
go przez HTTP w CI.

---

## Aktualizacja z 1.x

Przechodzisz z 1.x na 2.0? Zmiany wymagane w kodzie aplikacji — obsługa awarii bramki płatności, wymagane pola `PaymentRequest`, webhooki Tpay i Adyen — zebrano na stronie [Aktualizacja do 2.0](Migration/v2.md).

Linia **3.0** jest w fazie testowej (`3.0.0-preview.N`): biblioteki mediatora przechodzą na ForkForward.Mediator i zmieniają nazwę z `TailoredApps.Shared.MediatR*` na `TailoredApps.Shared.Mediator*`. Co zmienić w aplikacji: [Aktualizacja do 3.0](Migration/v3.md).

---

## Platformy docelowe

Od wersji 2.0 biblioteki są wydawane dla `net10.0` i `net11.0`; starsze platformy obsługują paczki 1.x (`1.6.x`). Assety `net11.0` są kompilowane z użyciem **runtime async**. Szczegóły, ograniczenia i sposób wyłączenia: [Platformy docelowe i runtime async](Platform/index.md).

---

## Szybki start

```bash
# Zainstaluj wybraną bibliotekę
dotnet add package TailoredApps.Shared.Mediator
dotnet add package TailoredApps.Shared.EntityFramework
dotnet add package TailoredApps.Shared.Payments
```

Pełna dokumentacja każdej biblioteki — łącznie z przykładami kodu, rejestracja DI i gotowymi promptami dla agentów AI — dostępna jest w sekcji **Libraries** w menu bocznym.

---

## Contributing

Zanim dodasz nową bibliotekę, przeczytaj [zasady contributingu](contributing.md) i [DOCUMENTATION_RULE](https://github.com/tailored-apps/SharedComponents/blob/master/DOCUMENTATION_RULE.md).

**Każda nowa biblioteka musi posiadać stronę dokumentacji — PR bez niej zostanie odrzucony.**
