# Sample service and integration tests

!!! info "3.0 line (test phase)"
    The sample is in the repository from the 3.0 line (`master`) and uses the Mediator libraries on **ForkForward.Mediator**
    (see [Upgrading to 3.0](../Migration/v3.md)). It is not a NuGet package and is never published to nuget.org.

`samples/` holds a small shop built the way a real service of ours is built: the contract, the logic and the HTTP host, with a browser
client (Blazor WebAssembly) and a .NET Aspire host around them. Together they use **all 26 libraries together** with [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator) and
[ForkForward.Mapper](https://www.nuget.org/packages/ForkForward.Mapper). It has a product catalogue, orders, payments through nine
gateways, gateway notifications and a confirmation e-mail. It runs with no account and no network: the payment gateways are faked,
e-mail goes to the console, and the database is SQLite in memory.

`tests/TailoredApps.Shared.IntegrationTests` starts the service in memory (`WebApplicationFactory`) and drives it over HTTP. CI runs
these tests with the rest of the solution, so a library change that breaks how the libraries work together stops the PR.

---

## Running it

```bash
dotnet run --project samples/TailoredApps.Shared.Demo.WebApi -f net10.0
```

The `http` profile in `Properties/launchSettings.json` listens on `http://localhost:5080` in the Development environment: the browser
client is at that address, the API under `/api`. For `net11.0` the sample is compiled with runtime async, as the libraries are
([Target frameworks](../Platform/index.md#runtime-async-net-11)).

With the Aspire dashboard (logs, traces and metrics of the service) - the same shop at the same address, the dashboard at
`http://localhost:15080`:

```bash
dotnet run --project samples/TailoredApps.Shared.Demo.AppHost
```

The integration tests:

```bash
dotnet test tests/TailoredApps.Shared.IntegrationTests/TailoredApps.Shared.IntegrationTests.csproj -f net10.0
```

---

## The projects

| Project | What it holds | Depends on |
|---|---|---|
| `TailoredApps.Shared.Demo.DataModel` | The contract. Per feature (`Catalog`, `Orders`): `Messages/Commands`, `Messages/Queries`, `Messages/Notifications`, `Responses/…`, `Model` | the mediator contracts, `Mediator.PagedRequest`, `Mediator.Caching`, `Payments` - no EF Core, no handlers |
| `TailoredApps.Shared.Demo.Services` | The logic. Per feature: `Entities`, `Handlers` (one interface each under `Interfaces/Handlers`), `Infrastructure` (repositories over the unit of work, behind `Interfaces/Infrastructure`), `Validation`, `Policies` (cache, retry, fallback), `Processing` (pre/post-processors, exception action and handler, stream behavior), `Exceptions`, the EF mapping (`IModelBuilder`) and the mapper profile. `ShopDataContext` at the root | `DataModel`, EF Core, the libraries - nothing of ASP.NET Core |
| `TailoredApps.Shared.Demo.WebApi` | The host. `Program` (builds, seeds, runs) and `Startup` (`ConfigureServices`, `Configure`), `Controllers`, `Endpoints` (minimal API), `Authentication`, `ExceptionHandling`. It also serves the client's files | both, and every library |
| `TailoredApps.Shared.Demo.Client` | The browser client: Blazor WebAssembly with MudBlazor, no JavaScript of its own. `Pages` (catalogue, product, basket, order), `Services` (`ShopApi`, `Session`, `Basket`) | `DataModel` only - the messages and responses are the server's own types |
| `TailoredApps.Shared.Demo.AppHost` | .NET Aspire: starts the Web API and the dashboard | the WebApi project |

A request travels one way: a **controller** turns HTTP into a message and sends it; the **mediator pipeline** validates, caches, retries;
a **handler** decides; a **repository** reads and writes through the unit of work and maps entities to the model; the **transaction
filter** commits or rolls back; the **exception provider** shapes the error. No layer reaches around the next one.

---

## Where each library is used

| Library | In the sample | How to see it |
|---|---|---|
| [WebApi](../Libraries/WebApi/index.md) | `AddTailoredWebApiDefaults()` and `MapTailoredWebApiDefaults()` in `Program.BuildApp` | `GET /health`, `GET /alive` |
| [DateTime](../Libraries/DateTime/index.md) | `AddDateTimeProvider()`; the order number, `CreatedAt`, `PaidAt` | the tests replace `TimeProvider` and check the times to the tick |
| [ExceptionHandling](../Libraries/ExceptionHandling/index.md) | `ShopExceptionHandlingProvider`: a `ShopException` answers with its own status and message, everything else with the defaults | `GET /api/products/999` (404), a duplicate product (409) |
| [EntityFramework](../Libraries/EntityFramework/index.md), [UnitOfWork.WebApiCore](../Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `ShopDataContext` with one `IModelBuilder` per feature, repositories on `IUnitOfWork<IShopDataContext>`, the transaction filter | `POST /api/orders` with a missing product: 400, and nothing is left in the database |
| [UnitOfWork.MinimalApi](../Libraries/EntityFramework/UnitOfWork.MinimalApi.md), [ExceptionHandling.MinimalApi](../Libraries/ExceptionHandling/MinimalApi.md) | `Endpoints/ProductEndpoints.cs`: a group with `WithExceptionHandling()`, `WithUnitOfWorkTransaction()` and `WithValidation<AddProduct>()`, sending the same messages | `POST /api/minimal/products`, `GET /api/minimal/products/{name}` |
| [Querying](../Libraries/Querying/index.md), [Mediator.PagedRequest](../Libraries/Mediator/PagedRequest.md) | `GetProducts` with `ProductFilter`; in `ProductRepository`: `ApplySorting` with an allow-list, the mapper's `ProjectTo`, `PagingAsync` | `GET /api/products?page=1&count=3&sortField=Price&sortDir=Desc&filter.category=tea` |
| [Mediator](../Libraries/Mediator/index.md), [Mediator.Caching](../Libraries/Mediator/Caching.md) | the five behaviors around every request; `GetProduct` (cache policy), `GetRecommendations` (retry, fallback) | `GET /api/products/1`, `GET /api/products/0`, `GET /api/products/1/recommendations` |
| [Payments](../Libraries/Payments/index.md) + 9 providers | `AddPayments().Register…Provider()`, `CheckoutOrderCommandHandler`, `ReceivePaymentNotificationCommandHandler` | `POST /api/orders/{number}/checkout`, `POST /api/webhooks/{provider}`, `GET /api/payments/providers` |
| [Email](../Libraries/Email/index.md), [Email.Models](../Libraries/Email/Models.md), [Mediator.Email](../Libraries/Mediator/Email.md) | `SendOrderConfirmation`: `SendMail` with the `EmailTemplates/order-confirmation.html` template, the console provider | the message on the console once the order is paid |
| [Email.Office365](../Libraries/Email/Office365.md) | `GET /api/inbox` - only with `Demo:Office365Inbox` and an Azure AD application | without configuration: 501 |
| [Mediator.ML](../Libraries/Mediator/ML.md) | `POST /api/images/classify` - only with `ImageClassification:ModelFilePath` | without a model: 501 |

---

## What it shows of the mediator and the mapper

| Feature | In the sample | Registered by |
|---|---|---|
| Request with a response | every query and command, e.g. `GetOrder` → `GetOrderResponse` | scanning the Services assembly |
| Request without a response | `CancelOrder : IRequest`, handler returns `Task` | scanning |
| Notification with several handlers | `OrderPaid` → `SendOrderConfirmation` (mail) and `RecordOrderPaid` (a synchronous `NotificationHandler<T>`) | scanning; `ForeachAwaitPublisher` |
| Stream request | `ExportProducts : IStreamRequest<Product>`; the controller returns `sender.CreateStream(...)` | scanning |
| Stream behavior | `CountExportedProducts` | `cfg.AddStreamBehavior<…>()` |
| Pre-processor | `NormalizeCustomerEmail` (trims and lower-cases before validation) | `cfg.AddRequestPreProcessor<…>()` - scanning does not find processors |
| Post-processor | `CountStartedPayments` (after a successful checkout) | `cfg.AddRequestPostProcessor<…>()` |
| Exception action | `CountGatewayOutages`: observes, the exception still becomes the 503 | scanning |
| Exception handler | `TreatMissingOrderAsCancelled`: replaces the exception with a response, so cancelling is idempotent | scanning |
| The library behaviors | Logging → Validation → Caching → Fallback → Retry, inside the processors | `PipelineRegistration`, after `AddMediator` |
| Mapping by convention | `Entities.Order` → `Model.Order`: the status enum becomes its name, the lines a collection, `ProductName` is flattened from `Product.Name` | `AddAutoMapper(assembly)`: the profiles of the Services assembly |
| An explicit member | `LineTotal` computed from `UnitPrice * Quantity`; `Id` ignored when a command becomes an entity | the profiles |
| Projection | `ProjectTo<Model.Product>()` in the paged list and in the export: the SQL selects the model's columns only | `IMapper.ConfigurationProvider` |
| Configuration check | `AssertConfigurationIsValid()` at start and in a test: a forgotten member fails there, not at the first request | `Program.PrepareDatabase` |

The order of a request, from the outside in: exception handlers and actions → pre-processors → post-processors → the five library
behaviors → the handler. Notifications and streams do not pass through request behaviors. The mapper is used in the sample only: no
library depends on it, and it is not NativeAOT-compatible (the registration for NativeAOT is shown by `tests/TailoredApps.Shared.AotSmoke`).

---

## Authorization

The service accepts JWT bearer tokens with its own issuer and audience, signed with `Jwt:Key` (at least 32 characters, or it refuses to
start). Changing the catalogue needs the `CatalogAdmin` role; orders are open. The key is in `appsettings.Development.json` - a placeholder
for local work; another environment brings its own as a secret. In a deployment the tokens come from the organisation's auth service; in
the Development environment only, `POST /api/dev/token` stands in for it.

---

## The browser client

Open `http://localhost:5080`. The client is a Blazor WebAssembly application served by the Web API itself, so the page and the API
share one origin and nothing needs CORS or a proxy.

- **One contract.** The client references `Demo.DataModel` and nothing else of the service. `ShopApi` sends `CreateOrder` and reads
  `GetProductsResponse` - the classes the controller receives and the handler returns. There is no generated client to keep in step.
- **The service does the work.** The catalogue table asks for a page, a sort order and a filter; paging and sorting happen in the
  database. A form shows the messages of the service's validators; the client has no validation rules of its own.
- **Signing in** is one button, *Sign in as catalogue administrator*: it takes a token from `POST /api/dev/token` and then the
  "Add a product" form appears. A deployed client would use the organisation's auth service (OIDC) instead.
- **Paying.** After *Start the payment* the order waits for the gateway. For Revolut the page has *Play the gateway: payment
  completed*: the service builds the notification Revolut would send, signs it with the configured secret and passes it through the
  same handler as the real webhook - signature check included. The order becomes paid and the confirmation is printed on the console.
- **One target framework, `net10.0`.** The browser runs on the Mono runtime, which in .NET 11 does not execute runtime-async methods,
  and the `net11.0` assets of this repository are compiled that way. The Web API that serves the client can be either framework.

---

## Aspire

`Demo.AppHost` starts the Web API and the Aspire dashboard with one command. `AddTailoredWebApiDefaults()` already exports logs,
traces and metrics over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; Aspire sets it, so the dashboard shows every request with its
spans, the structured log of the mediator's `LoggingBehavior`, and the metrics - among them the sample's own counters
(`demo.payments.started`, `demo.payments.gateway_outages`, `demo.orders.paid`, `demo.catalog.products_exported`).

- No container is started and nothing needs Docker: the database stays SQLite in memory.
- The Web API targets two frameworks, and `dotnet run` - what Aspire's `AddProject` uses - refuses such a project unless it is told
  which one. The AppHost therefore starts it as an executable with the framework named (`net11.0`; the other one with
  `-- --Shop:Framework net10.0`).
- The AppHost itself is a single-framework project (`net10.0`): the Aspire SDK does not support a cross-targeting build.
- The shop keeps its address, `http://localhost:5080`. When another stack on the machine already uses that port, pick another:
  `dotnet run --project samples/TailoredApps.Shared.Demo.AppHost -- --Shop:Port 5180`.
- Stop the AppHost with Ctrl+C: the orchestrator it started (`dcp`) ends with it. Do not end `dcp` processes by name - another
  Aspire application on the same machine has its own.

---

## A walk through the shop

Every command works in Git Bash and in a Linux or macOS shell (`openssl` computes the notification signature).

```bash
# Products: page 1, the three most expensive
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"

# The tea catalogue as a stream
curl "http://localhost:5080/api/products/export?category=tea"

# A new product needs the CatalogAdmin role: 401 without a token
token=$(curl -s -X POST "http://localhost:5080/api/dev/token?name=anna&role=CatalogAdmin" | sed -E 's/.*"token":"([^"]+)".*/\1/')
curl -X POST http://localhost:5080/api/products -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
  -d '{"name":"Matcha","category":"tea","price":89}'
# {"product":{"id":8,"name":"Matcha","category":"tea","price":89}}

# An order: two green teas and a teapot
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"Anna@Demo.Example","productIds":[1,1,6]}'
# {"order":{"number":"ORD-20261112-cbef5f49","customerEmail":"anna@demo.example","amount":178.8,"status":"New",
#   "lines":[{"productId":1,"productName":"Green tea","unitPrice":24.9,"quantity":2,"lineTotal":49.8},...]}}

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

# A paid order cannot be cancelled (409); a new one can (204)
curl -i -X DELETE http://localhost:5080/api/orders/ORD-20261112-cbef5f49
```

The notification timestamp has to be current: Revolut rejects signatures older than 300 seconds (`WebhookToleranceSeconds`).

---

## What is faked

- **Payment gateways** - `Payments/FakeGateway.cs` (WebApi project) is the primary handler of the providers' named HTTP clients
  (`AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(...)`) and answers the way each gateway does. The first path segment names
  the gateway: the `ServiceUrl` values in `appsettings.json` point to `https://gateway.demo/<gateway>`. Stripe works through its own SDK,
  so the sample registers an `IStripeClient` that uses the same handler. The provider libraries run unchanged - only the transport is replaced.
- **Gateway outage and refusal** - `FakeGatewayState.Unavailable` makes every gateway answer 503, and `FakeGatewayState.Refusing` 400;
  the tests check that checkout then answers 503 or 422 and the order stays as it was.
- **Real gateways** - set `Demo:UseFakeGateways` to `false` and put sandbox credentials in the configuration (user secrets preferably).
- **Notification signatures** are verified for real, with the placeholder keys of `appsettings.json`.
- **E-mail** - the console provider of the Email library; the tests replace it with a provider that records the messages.
- **The auth service** - `POST /api/dev/token`, mapped in the Development environment only.
- **The gateway's confirmation** - `POST /api/dev/gateway/revolut/orders/{number}/complete`, in the Development environment only: a
  correctly signed notification, delivered in process instead of over the network.
- **Database** - SQLite in memory on one open connection, seeded at start. SQLite has no `decimal` type, so amounts are stored in minor
  units and times in UTC ticks - exact, and sortable in SQL.

---

## The integration tests

| Class | What it checks |
|---|---|
| `WebApiDefaultsTests` | `/health` and `/alive` |
| `CatalogTests` | paging, sorting, the filter, parameter validation (400 naming the field; one Information entry without field values in the log, no Error), the cache (a second read does not run the handler), 404, retry and fallback |
| `OrdersTests` | saving and committing through the filter, rollback after a refused request, 404, checkout at all 9 gateways (with the HTTP request to the gateway), a gateway outage (503) and a refusal (422) - Stripe included - with the order unchanged |
| `WebhookTests` | a signed notification → the order is paid and one e-mail is sent from the template (also when the notification is repeated), a declined payment, a wrong signature and an old timestamp → 400 |
| `MinimalApiTests` | the minimal-API endpoints: a commit without `SaveChanges` in the endpoint, 400 with the validator's fields, 409 for a duplicate, 404 from the provider |
| `MediatorFeaturesTests` | 401 / 403 / a token signed with another key; the stream and its behavior; the mapper configuration, flattening and the computed member; the pre-processor; the post-processor and the exception action; a request without a response and the exception handler; both notification handlers, also when the mail cannot be sent; no signing key → the service does not start |
| `ClientHostingTests` | the Web API serves the client: its host page for the client's addresses, its files as files, every assembly named in the client's boot configuration; an unknown address under `/api` stays 404; the Development gateway endpoint pays an order through the signed notification; outside Development neither that endpoint nor the token endpoint exists |
| `ApplicationTests` | the providers and channels, 501 for ML and Office365 when they are not configured |
| `RuntimeAsyncTests` | how the async methods of the three sample assemblies and of the libraries were compiled |

The tests change two things in the service: the clock (`TimeProvider`, frozen at a fixed instant) and the e-mail provider. Everything
else is the service's own configuration.

---

## Lessons for your own service

- Controllers send a message and return the response; they hold no logic, catch nothing and never save.
- Nothing calls `SaveChanges` to finish a request: the transaction filter commits after a successful action and rolls back after a
  4xx/5xx result or an exception. A repository saves inside the transaction only to learn a generated key.
- The service throws its own exception types for what it refuses on purpose; the exception provider turns those into the status and
  message, and everything else into a 500 with a generic text. A transport error never reaches the client.
- The checkout handler turns `HttpRequestException` (including `PaymentGatewayException`) into one "gateway unavailable" exception (503):
  that one block covers all nine providers, Stripe included. A refusal (`Rejected`) is 422.
- Gateway notifications go to one endpoint with the raw body; the answer is the provider's `ResponseObject` (Tpay: `TRUE`, Adyen:
  `[accepted]`). A repeated notification changes nothing.
- A notification handler that talks to something outside (mail) catches its own failure: with the default publisher an exception stops
  the remaining handlers and fails the code that published.
- Pre- and post-processors are registered by name; scanning does not find them.
- A validator checks the paging and sorting parameters (400 naming the field), and the request has a default page and sort order,
  because without them the query extensions return the whole table in no particular order.
- Requests are validated in one place (FluentValidation in the pipeline); MVC's implicit "required" for non-nullable strings is off.
- A client written in .NET shares the contract project instead of generating a client from OpenAPI; it shows the service's error
  messages and keeps no rules of its own.
- When the host serves a Blazor WebAssembly client, the static files come before routing, and the client's fallback route must not
  swallow unknown API addresses.
- Check such a client in a browser, or ask the way a browser does. The sample turns off gzip copies during a build
  (`DisableBuildCompression`, in the client and in the host): after an incremental build the gzip copy of the client's boot
  configuration kept the previous list of assemblies, which a plain request never showed and a browser could not load.

---

## 🤖 AI Agent Prompt

````text
Use the TailoredApps sample service (samples/TailoredApps.Shared.Demo.DataModel, .Services, .WebApi in the SharedComponents
repository) as the reference for a Web API built on the TailoredApps.Shared.* libraries, ForkForward.Mediator and ForkForward.Mapper.

Structure:
- DataModel: messages (Commands, Queries, Notifications), responses and models per feature. No EF Core, no handlers.
- Services: entities, the data context, repositories on IUnitOfWork<IMyDataContext>, handlers, validators, policies, mapper profiles.
  No ASP.NET Core.
- WebApi: Program (BuildApp, database preparation), Startup (ConfigureServices, Configure), controllers, the exception provider.
- Client (optional): Blazor WebAssembly on net10.0 referencing DataModel only, served by the WebApi (UseBlazorFrameworkFiles,
  UseStaticFiles, then UseRouting; MapFallbackToFile("index.html") plus MapFallback("/api/{**path}", 404)).
- AppHost (optional): .NET Aspire, single target framework; a multi-targeted Web API is started with
  AddExecutable("dotnet", "run", "--project", path, "--framework", tfm) and WithOtlpExporter().

Registration (Startup.ConfigureServices, in this order):
- builder.AddTailoredWebApiDefaults() in Program; services.AddDateTimeProvider();
- services.AddDbContext<MyDataContext>(...); services.AddUnitOfWorkForWebApi<IMyDataContext, MyDataContext>();
- services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();
- services.AddControllers(o => { o.Filters.AddUnitOfWorkTransactionAttribute(); o.Filters.AddExceptionHandlingFilterAttribute(); });
- services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...); services.AddAuthorization();
- repositories: services.AddTransient<IMyRepository, MyRepository>();
- services.AddAutoMapper(servicesAssembly);
- services.AddMediator(cfg => { cfg.RegisterServicesFromAssembly(servicesAssembly);
    cfg.AddRequestPreProcessor<MyPreProcessor>(); cfg.AddRequestPostProcessor<MyPostProcessor>(); cfg.AddStreamBehavior<MyStreamBehavior>(); });
  var pipeline = new PipelineRegistration(services); pipeline.RegisterPipelineBehaviors(); pipeline.RegisterPipelineBehaviors(servicesAssembly);
- services.AddValidatorsFromAssemblyContaining<MyDataContext>();
- services.AddPayments().RegisterRevolutProvider()...; e-mail: RegisterConsoleProvider() or the SMTP provider, plus IMailMessageBuilder.
- Configure: app.ConfigureExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
  then app.MapTailoredWebApiDefaults() in Program.

What gets configured: health endpoints, OpenTelemetry, HTTP client resilience; one TimeProvider clock; JSON error responses;
a unit of work committed or rolled back per request; the mediator pipeline (exception handlers and actions, processors, then
logging, validation, caching, fallback, retry); entity-to-model mapping and projection; payment providers behind IPaymentService;
template e-mail; JWT bearer authentication.

Configuration: WebApiDefaults, Jwt (Key as a secret, at least 32 characters), Payments:Providers:<Provider>, Mail:Providers:<Provider>;
the sample's appsettings.json lists every key.

Rules:
- A controller action only sends a message with ISender and returns the response; put [ProducesResponseType] on it.
- Never call SaveChanges to finish a request; throw one of the service's own exceptions to refuse it and roll it back.
- Derive refusals from one base exception that carries the HTTP status; map only that base in the exception provider.
- Catch HttpRequestException around RegisterPayment and GetStatus and throw the service's "gateway unavailable" exception (503);
  answer PaymentStatus == Rejected with 422.
- Pass the raw request body to HandleWebhookAsync and answer with PaymentResponse.ResponseObject; make the handling idempotent.
- Put the application's own order number in PaymentRequest.AdditionalData.
- Register pre- and post-processors and stream behaviors by name; scanning registers handlers, exception handlers and actions only.
- A notification handler that can fail for outside reasons catches and logs; do not let it fail the publisher.
- Map with profiles and ProjectTo; call AssertConfigurationIsValid() at start. Do not use the mapper in a NativeAOT application.
- Validate paging and sorting parameters with FluentValidation and give paged requests a default page and sort order.
- Use `using ForkForward.Mediator;` / AddMediator and `using ForkForward.Mapper;` / AddAutoMapper, never the MediatR or AutoMapper packages.
- Never put a signing key in appsettings.json; fail at start when it is missing.
- Map endpoints that stand in for outside systems (token issuing, gateway confirmation) in the Development environment only.
- Do not reference net11.0 assets compiled with runtime async from a Blazor WebAssembly project: target net10.0 there.
````
