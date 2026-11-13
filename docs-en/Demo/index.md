# Demo application and integration tests

!!! info "3.0 line (test phase)"
    The demo is in the repository from the 3.0 line (`master`) and uses the MediatR libraries on **ForkForward.Mediator**
    (see [Upgrading to 3.0](../Migration/v3.md)). It is not a NuGet package and is never published to nuget.org.

`samples/TailoredApps.Shared.Demo` is a small shop built as an ASP.NET Core Web API that uses **all 24 libraries together**: a product
catalogue, orders, payments through nine gateways, gateway notifications and a confirmation e-mail. It runs with no account and no
network: the payment gateways are faked, e-mail goes to the console, and the database is SQLite in memory.

`tests/TailoredApps.Shared.IntegrationTests` starts the application in memory (`WebApplicationFactory`) and drives it over HTTP. CI runs
these tests with the rest of the solution, so a library change that breaks how the libraries work together stops the PR.

---

## Running it

```bash
dotnet run --project samples/TailoredApps.Shared.Demo -f net10.0
```

The `http` profile in `Properties/launchSettings.json` listens on `http://localhost:5080` in the Development environment. For `net11.0` the
demo is compiled with runtime async, as the libraries are ([Target frameworks](../Platform/index.md#runtime-async-net-11)).

The integration tests:

```bash
dotnet test tests/TailoredApps.Shared.IntegrationTests/TailoredApps.Shared.IntegrationTests.csproj -f net10.0
```

---

## Where each library is used

| Library | In the demo | How to see it |
|---|---|---|
| [WebApi](../Libraries/WebApi/index.md) | `AddTailoredWebApiDefaults()` and `MapTailoredWebApiDefaults()` in `Program.cs` | `GET /health`, `GET /alive` |
| [DateTime](../Libraries/DateTime/index.md) | `AddDateTimeProvider()`; the order number, `CreatedAt`, `PaidAt` | the tests replace `TimeProvider` and check the times to the tick |
| [ExceptionHandling](../Libraries/ExceptionHandling/index.md) | `DemoExceptionHandlingProvider` (missing order or product: 404), `ConfigureExceptionHandler()` | `GET /api/products/999` |
| [EntityFramework](../Libraries/EntityFramework/index.md), [UnitOfWork.WebApiCore](../Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `ShopDbContext`, `AddUnitOfWorkForWebApi<,>()`, the transaction filter - no action calls `SaveChanges` | `POST /api/orders` with a missing product: 400, and nothing is left in the database |
| [Querying](../Libraries/Querying/index.md), [MediatR.PagedRequest](../Libraries/MediatR/PagedRequest.md) | `GetProducts` with `ProductFilter`, `ApplySorting` with an allow-list, `PagingAsync` | `GET /api/products?page=1&count=3&sortField=Price&sortDir=Desc&filter.category=tea` |
| [MediatR](../Libraries/MediatR/index.md), [MediatR.Caching](../Libraries/MediatR/Caching.md) | `GetProduct` (validation, HybridCache), `GetRecommendations` (retry, fallback) | `GET /api/products/1`, `GET /api/products/0`, `GET /api/products/1/recommendations` |
| [Payments](../Libraries/Payments/index.md) + 9 providers | `AddPayments().Register…Provider()`, `OrdersController.Checkout`, `WebhooksController` | `POST /api/orders/{number}/checkout`, `POST /api/webhooks/{provider}`, `GET /api/payments/providers` |
| [Email](../Libraries/Email/index.md), [Email.Models](../Libraries/Email/Models.md), [MediatR.Email](../Libraries/MediatR/Email.md) | the order confirmation: `SendMail` with the `EmailTemplates/order-confirmation.html` template, the console provider | the message on the console once the order is paid |
| [Email.Office365](../Libraries/Email/Office365.md) | `GET /api/inbox` - only with `Demo:Office365Inbox` and an Azure AD application | without configuration: 501 |
| [MediatR.ML](../Libraries/MediatR/ML.md) | `POST /api/images/classify` - only with `ImageClassification:ModelFilePath` | without a model: 501 |

---

## A walk through the shop

Every command works in Git Bash and in a Linux or macOS shell (`openssl` computes the notification signature).

```bash
# Products: page 1, the three most expensive
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"

# An order: green tea and a teapot
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"anna@demo.example","productIds":[1,6]}'
# {"number":"ORD-20261112-cbef5f49","amount":153.9,"status":"New",...}

# A Revolut payment: the address of the payment page and the payment id
curl -X POST http://localhost:5080/api/orders/ORD-20261112-cbef5f49/checkout -H "Content-Type: application/json" \
  -d '{"provider":"Revolut","channel":"card"}'
# {"number":"ORD-20261112-cbef5f49","paymentId":"REVOLUT-1","redirectUrl":"https://gateway.demo/pay/revolut/REVOLUT-1"}

# The payment notification, signed the way Revolut signs it (the secret from appsettings.json)
body='{"event":"ORDER_COMPLETED","order_id":"REVOLUT-1"}'
ts=$(( $(date +%s) * 1000 ))
sig=$(printf 'v1.%s.%s' "$ts" "$body" | openssl dgst -sha256 -hmac 'demo-not-a-real-secret' | sed 's/^.*= //')
curl -X POST http://localhost:5080/api/webhooks/Revolut -H "Content-Type: application/json" \
  -H "Revolut-Request-Timestamp: $ts" -H "Revolut-Signature: v1=$sig" -d "$body"
# OK - and on the application console: recipent: anna@demo.example; topic: Order ORD-20261112-cbef5f49 is paid; ...

curl http://localhost:5080/api/orders/ORD-20261112-cbef5f49
# "status":"Paid","paidAt":"..."
```

The notification timestamp has to be current: Revolut rejects signatures older than 300 seconds (`WebhookToleranceSeconds`).

---

## What is faked

- **Payment gateways** - `Payments/FakeGateway.cs` is the primary handler of the providers' named HTTP clients
  (`AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(...)`) and answers the way each gateway does. The first path segment names
  the gateway: the `ServiceUrl` values in `appsettings.json` point to `https://gateway.demo/<gateway>`. Stripe works through its own SDK,
  so the demo registers an `IStripeClient` that uses the same handler. The provider libraries run unchanged - only the transport is replaced.
- **Gateway outage and refusal** - `FakeGatewayState.Unavailable` makes every gateway answer 503, and `FakeGatewayState.Refusing` 400;
  the tests check that checkout then answers 503 or 422 and the order stays as it was.
- **Real gateways** - set `Demo:UseFakeGateways` to `false` and put sandbox credentials in the configuration (user secrets preferably).
- **Notification signatures** are verified for real, with the placeholder keys of `appsettings.json`.
- **E-mail** - the console provider of the Email library; the tests replace it with a provider that records the messages.
- **Database** - SQLite in memory on one open connection, seeded at start. SQLite has no `decimal` type, so amounts are stored in minor
  units and times in UTC ticks - exact, and sortable in SQL.

---

## The integration tests

| Class | What it checks |
|---|---|
| `WebApiDefaultsTests` | `/health` and `/alive` |
| `CatalogTests` | paging, sorting, the filter, parameter validation (400 naming the field; one Information entry without field values in the log, no Error), the cache (a second read does not run the handler), 404, retry and fallback |
| `OrdersTests` | saving and committing through the filter, rollback after 400, 404, checkout at all 9 gateways (with the HTTP request to the gateway), a gateway outage (503) and a refusal (422) - Stripe included - with the order unchanged |
| `WebhookTests` | a signed notification → the order is paid and one e-mail is sent from the template (also when the notification is repeated), a declined payment, a wrong signature and an old timestamp → 400 |
| `ApplicationTests` | the providers and channels, 501 for ML and Office365 when they are not configured |

The tests change two things in the application: the clock (`TimeProvider`, frozen at a fixed instant) and the e-mail provider. Everything
else is the application's own configuration. Code coverage from these tests counts for the libraries; the demo assembly is excluded.

---

## Lessons for your own application

- Actions do not call `SaveChanges`: the transaction filter saves and commits after a successful action and rolls back after a 4xx/5xx
  result or an exception.
- Checkout catches `HttpRequestException` (including `PaymentGatewayException`) and answers 503 - the payment may or may not exist.
  That one block covers all nine providers, Stripe included; the demo turns a gateway refusal (`Rejected`) into 422.
- Gateway notifications go to one endpoint with the raw body; the answer is the provider's `ResponseObject` (Tpay: `TRUE`, Adyen:
  `[accepted]`). A repeated notification changes nothing.
- `PaymentRequest.AdditionalData` carries the application's own order number - Tpay and Adyen identify the payment by it in the notification.
- A validator checks the paging and sorting parameters (400 naming the field), and the request has a default page and sort order,
  because without them the query extensions return the whole table in no particular order.
- Values shown to the customer (the amount in the e-mail) are formatted independently of the server's culture.

---

## 🤖 AI Agent Prompt

````text
Use the TailoredApps demo (samples/TailoredApps.Shared.Demo in the SharedComponents repository) as the reference for wiring
TailoredApps.Shared.* libraries into an ASP.NET Core Web API.

Registration (Program.cs, in this order):
- builder.AddTailoredWebApiDefaults(); builder.Services.AddDateTimeProvider();
- builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();
- builder.Services.AddDbContext<MyDbContext>(...); builder.Services.AddUnitOfWorkForWebApi<IMyDbContext, MyDbContext>();
- builder.Services.AddControllers(o => { o.Filters.AddUnitOfWorkTransactionAttribute(); o.Filters.AddExceptionHandlingFilterAttribute(); });
- builder.Services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
  var pipeline = new PipelineRegistration(builder.Services); pipeline.RegisterPipelineBehaviors(); pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);
- validators: builder.Services.AddTransient<IValidator<TRequest>, TRequestValidator>();
- builder.Services.AddPayments().RegisterRevolutProvider()...; e-mail: RegisterConsoleProvider() or the SMTP provider, plus IMailMessageBuilder.
- app.ConfigureExceptionHandler(); app.MapTailoredWebApiDefaults(); app.MapControllers();

What gets configured: health endpoints, OpenTelemetry, HTTP client resilience; one TimeProvider clock; JSON error responses;
a unit of work committed or rolled back per request; the MediatR pipeline (logging, validation, caching, fallback, retry);
payment providers behind IPaymentService; template e-mail.

Configuration: WebApiDefaults, Payments:Providers:<Provider>, Mail:Providers:<Provider>; the demo's appsettings.json lists every key.

Rules:
- Never call SaveChanges in a controller action; return a 4xx result to roll the request back.
- Wrap RegisterPayment and GetStatus in catch (HttpRequestException) and answer 503 (every provider, Stripe included); answer
  PaymentStatus == Rejected with 422.
- Pass the raw request body to HandleWebhookAsync and answer with PaymentResponse.ResponseObject; make the handling idempotent.
- Put the application's own order number in PaymentRequest.AdditionalData.
- Validate paging and sorting parameters with FluentValidation and give paged requests a default page and sort order.
- Use `using ForkForward.Mediator;` and AddMediator (3.0 line), never the MediatR package.
````
