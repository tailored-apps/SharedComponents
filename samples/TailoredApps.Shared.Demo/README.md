# TailoredApps.Shared.Demo

A small shop Web API that uses all 26 `TailoredApps.Shared.*` libraries together: a product catalogue, orders, payments
through nine gateways, gateway notifications and a confirmation e-mail. It runs with no account and no network: the
payment gateways are faked, e-mail goes to the console, and the database is SQLite in memory.

It is not a NuGet package. `tests/TailoredApps.Shared.IntegrationTests` starts it in memory and drives it over HTTP; CI
runs those tests with the rest of the solution.

## Run

```bash
dotnet run --project samples/TailoredApps.Shared.Demo -f net10.0
```

The `http` launch profile listens on `http://localhost:5080` in the Development environment.

```bash
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"anna@demo.example","productIds":[1,6]}'
curl -X POST http://localhost:5080/api/orders/<number>/checkout -H "Content-Type: application/json" \
  -d '{"provider":"Revolut","channel":"card"}'
```

## Where each library is used

| Library | In the demo |
|---|---|
| WebApi | `AddTailoredWebApiDefaults()` / `MapTailoredWebApiDefaults()` in `Program.cs`: `/health`, `/alive`, HTTP client resilience |
| DateTime | `AddDateTimeProvider()`; order numbers, `CreatedAt`, `PaidAt` |
| ExceptionHandling | `DemoExceptionHandlingProvider` (missing order or product: 404), `ConfigureExceptionHandler()` |
| EntityFramework, EntityFramework.UnitOfWork.WebApiCore | `ShopDbContext`, `AddUnitOfWorkForWebApi<,>()`, the transaction filter: no action calls `SaveChanges` |
| EntityFramework.UnitOfWork.MinimalApi, ExceptionHandling.MinimalApi | `Catalog/ProductEndpoints.cs`: minimal-API endpoints with the transaction, validation and exception filters |
| Querying, MediatR.PagedRequest | `GetProducts` with `ProductFilter`, sorted and paged by the EntityFramework query extensions |
| MediatR, MediatR.Caching | `GetProduct` (validation, HybridCache), `GetRecommendations` (retry, fallback) |
| Payments + 9 providers | `AddPayments().Register…Provider()`, `OrdersController.Checkout`, `WebhooksController` |
| Email, Email.Models, MediatR.Email | the order confirmation: `SendMail` with `EmailTemplates/order-confirmation.html`, console provider |
| Email.Office365 | `GET /api/inbox`, only with `Demo:Office365Inbox` and an Azure AD application; otherwise 501 |
| MediatR.ML | `POST /api/images/classify`, only with `ImageClassification:ModelFilePath`; otherwise 501 |

## What is faked

- **Payment gateways** - `Payments/FakeGateway.cs` is the primary handler of the providers' named HTTP clients and answers
  the way each gateway does; the first segment of the path names the gateway (`ServiceUrl` values in `appsettings.json`).
  Stripe's SDK gets an `IStripeClient` that uses the same fake. `FakeGatewayState` makes every gateway fail (503) or
  refuse (400), for the outage and refusal paths. Set `Demo:UseFakeGateways` to `false` and configure
  sandbox credentials to talk to the real gateways.
- **Gateway notifications** are verified for real, with the placeholder secrets of `appsettings.json`.
- **E-mail** - the console provider prints the message.

Documentation: <https://shared.tailoredapps.pl/en/Demo/>
