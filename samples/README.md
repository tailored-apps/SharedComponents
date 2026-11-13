# Sample service

A small shop built the way a real service is built on the `TailoredApps.Shared.*` libraries, with
[ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator) and
[ForkForward.Mapper](https://www.nuget.org/packages/ForkForward.Mapper): a product catalogue, orders, payments through nine
gateways, gateway notifications and a confirmation e-mail. It uses all 26 libraries and runs with no account and no network: the
payment gateways are faked, e-mail goes to the console, and the database is SQLite in memory.

It is not a NuGet package. `tests/TailoredApps.Shared.IntegrationTests` starts it in memory and drives it over HTTP; CI runs
those tests with the rest of the solution.

## The three projects

| Project | Holds |
|---|---|
| `TailoredApps.Shared.Demo.DataModel` | The contract: messages (`Messages/Commands`, `Queries`, `Notifications`), `Responses`, `Model`, per feature. No EF Core, no handlers |
| `TailoredApps.Shared.Demo.Services` | The logic: `Entities`, `Handlers` and their interfaces, repositories over the unit of work, `Validation`, `Policies`, `Processing`, `Exceptions`, EF and mapper mappings, `ShopDataContext`. No ASP.NET Core |
| `TailoredApps.Shared.Demo.WebApi` | The host: `Program`, `Startup`, `Controllers`, `Endpoints` (minimal API), `Authentication`, `ExceptionHandling` |

A controller turns HTTP into a message and sends it; the mediator pipeline validates, caches and retries; a handler decides; a
repository reads and writes through the unit of work and maps entities to the model; the transaction filter commits or rolls back;
the exception provider shapes the error.

## Run

```bash
dotnet run --project samples/TailoredApps.Shared.Demo.WebApi -f net10.0
```

The `http` launch profile listens on `http://localhost:5080` in the Development environment.

```bash
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"
curl "http://localhost:5080/api/products/export?category=tea"
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"anna@demo.example","productIds":[1,1,6]}'
curl -X POST http://localhost:5080/api/orders/<number>/checkout -H "Content-Type: application/json" \
  -d '{"provider":"Revolut","channel":"card"}'

# Changing the catalogue needs the CatalogAdmin role; in Development a token can be issued locally
token=$(curl -s -X POST "http://localhost:5080/api/dev/token?name=anna&role=CatalogAdmin" | sed -E 's/.*"token":"([^"]+)".*/\1/')
curl -X POST http://localhost:5080/api/products -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
  -d '{"name":"Matcha","category":"tea","price":89}'
```

## What it shows

- **The libraries** - WebApi defaults, one clock, the unit of work with the transaction filter (MVC and minimal API), exception
  handling with the service's own exception types, paging and sorting, the five mediator behaviors (logging, validation, caching,
  fallback, retry), payments with nine providers, template e-mail; Office365 and ML only when configured (501 otherwise).
- **The mediator** - requests with and without a response, a notification with two handlers, a stream request with a stream
  behavior, a pre-processor, a post-processor, an exception action and an exception handler.
- **The mapper** - mapping by convention (enum to name, collections, flattening), an explicit computed member, projection of a
  query (`ProjectTo`), and a configuration check at start.
- **Authorization** - JWT bearer tokens signed with `Jwt:Key`; the service refuses to start without a key of 32 characters.

## What is faked

- **Payment gateways** - `Payments/FakeGateway.cs` in the WebApi project is the primary handler of the providers' named HTTP
  clients and answers the way each gateway does; the first segment of the path names the gateway (`ServiceUrl` values in
  `appsettings.json`). Stripe's SDK gets an `IStripeClient` that uses the same fake. `FakeGatewayState` makes every gateway fail
  (503) or refuse (400), for the outage and refusal paths. Set `Demo:UseFakeGateways` to `false` and configure sandbox credentials
  to talk to the real gateways.
- **Gateway notifications** are verified for real, with the placeholder secrets of `appsettings.json`.
- **E-mail** - the console provider prints the message.
- **The auth service** - `POST /api/dev/token`, in the Development environment only.

Documentation: <https://shared.tailoredapps.pl/en/Demo/>
