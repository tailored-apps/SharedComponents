# Aplikacja demo i testy integracyjne

!!! info "Linia 3.0 (faza testowa)"
    Demo jest w repozytorium od linii 3.0 (`master`) i korzysta z bibliotek MediatR na **ForkForward.Mediator**
    (zob. [Aktualizacja do 3.0](../Migration/v3.md)). Nie jest paczką NuGet — nie trafia na nuget.org.

`samples/TailoredApps.Shared.Demo` to mały sklep w ASP.NET Core Web API, który używa **wszystkich 24 bibliotek naraz**: katalog
produktów, zamówienia, płatności przez dziewięć bramek, powiadomienia z bramek i mail z potwierdzeniem. Działa bez kont i bez sieci:
bramki płatności są podstawione, poczta trafia na konsolę, a baza to SQLite w pamięci.

`tests/TailoredApps.Shared.IntegrationTests` uruchamia tę aplikację w pamięci (`WebApplicationFactory`) i sprawdza ją przez HTTP.
CI uruchamia te testy razem z resztą rozwiązania, więc zmiana w bibliotece, która psuje współpracę bibliotek, zatrzymuje PR.

---

## Uruchomienie

```bash
dotnet run --project samples/TailoredApps.Shared.Demo -f net10.0
```

Profil `http` z `Properties/launchSettings.json` nasłuchuje na `http://localhost:5080` w środowisku Development.

Testy integracyjne:

```bash
dotnet test tests/TailoredApps.Shared.IntegrationTests/TailoredApps.Shared.IntegrationTests.csproj -f net10.0
```

---

## Gdzie jest która biblioteka

| Biblioteka | W demo | Jak zobaczyć |
|---|---|---|
| [WebApi](../Libraries/WebApi/index.md) | `AddTailoredWebApiDefaults()` i `MapTailoredWebApiDefaults()` w `Program.cs` | `GET /health`, `GET /alive` |
| [DateTime](../Libraries/DateTime/index.md) | `AddDateTimeProvider()`; numer zamówienia, `CreatedAt`, `PaidAt` | testy podmieniają `TimeProvider` i sprawdzają czasy co do ticka |
| [ExceptionHandling](../Libraries/ExceptionHandling/index.md) | `DemoExceptionHandlingProvider` (brak zamówienia lub produktu → 404), `ConfigureExceptionHandler()` | `GET /api/products/999` |
| [EntityFramework](../Libraries/EntityFramework/index.md), [UnitOfWork.WebApiCore](../Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `ShopDbContext`, `AddUnitOfWorkForWebApi<,>()`, filtr transakcji — żadna akcja nie woła `SaveChanges` | `POST /api/orders` z nieistniejącym produktem: 400 i nic nie zostaje w bazie |
| [Querying](../Libraries/Querying/index.md), [MediatR.PagedRequest](../Libraries/MediatR/PagedRequest.md) | `GetProducts` z `ProductFilter`, `ApplySorting` z listą dozwolonych pól, `PagingAsync` | `GET /api/products?page=1&count=3&sortField=Price&sortDir=Desc&filter.category=tea` |
| [MediatR](../Libraries/MediatR/index.md), [MediatR.Caching](../Libraries/MediatR/Caching.md) | `GetProduct` (walidacja, HybridCache), `GetRecommendations` (retry, fallback) | `GET /api/products/1`, `GET /api/products/0`, `GET /api/products/1/recommendations` |
| [Payments](../Libraries/Payments/index.md) + 9 dostawców | `AddPayments().Register…Provider()`, `OrdersController.Checkout`, `WebhooksController` | `POST /api/orders/{numer}/checkout`, `POST /api/webhooks/{dostawca}`, `GET /api/payments/providers` |
| [Email](../Libraries/Email/index.md), [Email.Models](../Libraries/Email/Models.md), [MediatR.Email](../Libraries/MediatR/Email.md) | potwierdzenie zamówienia: `SendMail` z szablonem `EmailTemplates/order-confirmation.html`, dostawca konsolowy | mail na konsoli po opłaceniu zamówienia |
| [Email.Office365](../Libraries/Email/Office365.md) | `GET /api/inbox` — tylko z `Demo:Office365Inbox` i aplikacją Azure AD | bez konfiguracji: 501 |
| [MediatR.ML](../Libraries/MediatR/ML.md) | `POST /api/images/classify` — tylko z `ImageClassification:ModelFilePath` | bez modelu: 501 |

---

## Przejście przez sklep

Każde polecenie działa w Git Bash i w powłoce Linuksa lub macOS (`openssl` liczy podpis powiadomienia).

```bash
# Produkty: strona 1, trzy najdroższe
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"

# Zamówienie: zielona herbata i czajniczek
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"anna@demo.example","productIds":[1,6]}'
# {"number":"ORD-20261112-cbef5f49","amount":153.9,"status":"New",...}

# Płatność w Revolut: adres strony płatności i identyfikator płatności
curl -X POST http://localhost:5080/api/orders/ORD-20261112-cbef5f49/checkout -H "Content-Type: application/json" \
  -d '{"provider":"Revolut","channel":"card"}'
# {"number":"ORD-20261112-cbef5f49","paymentId":"REVOLUT-1","redirectUrl":"https://gateway.demo/pay/revolut/REVOLUT-1"}

# Powiadomienie o zapłacie, podpisane tak jak podpisuje je Revolut (sekret z appsettings.json)
body='{"event":"ORDER_COMPLETED","order_id":"REVOLUT-1"}'
ts=$(( $(date +%s) * 1000 ))
sig=$(printf 'v1.%s.%s' "$ts" "$body" | openssl dgst -sha256 -hmac 'demo-not-a-real-secret' | sed 's/^.*= //')
curl -X POST http://localhost:5080/api/webhooks/Revolut -H "Content-Type: application/json" \
  -H "Revolut-Request-Timestamp: $ts" -H "Revolut-Signature: v1=$sig" -d "$body"
# OK — a na konsoli aplikacji: recipent: anna@demo.example; topic: Order ORD-20261112-cbef5f49 is paid; ...

curl http://localhost:5080/api/orders/ORD-20261112-cbef5f49
# "status":"Paid","paidAt":"..."
```

Znacznik czasu powiadomienia musi być aktualny: Revolut odrzuca podpisy starsze niż 300 sekund (`WebhookToleranceSeconds`).

---

## Co jest podstawione

- **Bramki płatności** — `Payments/FakeGateway.cs` jest głównym handlerem nazwanych klientów HTTP dostawców
  (`AddHttpClient(nazwa).ConfigurePrimaryHttpMessageHandler(...)`) i odpowiada tak jak każda z bramek. Pierwszy segment ścieżki
  wskazuje bramkę: wartości `ServiceUrl` w `appsettings.json` prowadzą do `https://gateway.demo/<bramka>`. Stripe działa przez własne
  SDK, więc demo rejestruje `IStripeClient` korzystający z tego samego handlera. Biblioteki dostawców działają bez zmian — podstawiony jest
  tylko transport.
- **Awaria i odmowa bramki** — `FakeGatewayState.Unavailable` sprawia, że każda bramka odpowiada 503, a `FakeGatewayState.Refusing` — że
  odpowiada 400; testy sprawdzają, że checkout odpowiada wtedy 503 albo 422, a zamówienie zostaje bez zmian.
- **Prawdziwe bramki** — `Demo:UseFakeGateways` ustawione na `false` i dane sandboxów w konfiguracji (najlepiej w user secrets).
- **Podpisy powiadomień** są sprawdzane naprawdę, kluczami-zaślepkami z `appsettings.json`.
- **Poczta** — dostawca konsolowy z biblioteki Email; testy podmieniają go na dostawcę zapisującego wiadomości.
- **Baza** — SQLite w pamięci na jednym otwartym połączeniu, wypełniana przy starcie. SQLite nie ma typu `decimal`, więc kwoty są
  zapisane w groszach, a czasy w tickach UTC — dokładnie i z sortowaniem w SQL.

---

## Testy integracyjne

| Klasa | Co sprawdza |
|---|---|
| `WebApiDefaultsTests` | `/health` i `/alive` |
| `CatalogTests` | stronicowanie, sortowanie, filtr, walidację parametrów (400 z nazwą pola; w logu wpis Information bez wartości pól, żadnego Error), cache (drugi odczyt nie uruchamia handlera), 404, retry i fallback |
| `OrdersTests` | zapis i commit przez filtr, rollback po 400, 404, checkout we wszystkich 9 bramkach (z żądaniem HTTP do bramki), awarię bramki (503) i odmowę (422) — także dla Stripe — z zamówieniem bez zmian |
| `WebhookTests` | podpisane powiadomienie → zamówienie opłacone i jeden mail z szablonu (także przy powtórzonym powiadomieniu), płatność odrzucona, zły podpis i stary znacznik czasu → 400 |
| `ApplicationTests` | lista dostawców i kanałów, 501 dla nieskonfigurowanych ML i Office365 |

Testy zmieniają w aplikacji dwie rzeczy: zegar (`TimeProvider`, zamrożony na stałej chwili) i dostawcę poczty. Wszystko inne to
konfiguracja samej aplikacji. Pokrycie kodu z tych testów liczy się dla bibliotek; zestaw demo jest z niego wyłączony.

---

## Wnioski dla własnej aplikacji

- Akcje nie wołają `SaveChanges`: filtr transakcji zapisuje i zatwierdza po udanej akcji, a wycofuje po odpowiedzi 4xx/5xx lub wyjątku.
- Checkout łapie `HttpRequestException` (w tym `PaymentGatewayException`) i odpowiada 503 — płatność mogła powstać albo nie.
  Ten jeden blok obejmuje wszystkich dziewięciu dostawców, także Stripe; odmowę bramki (`Rejected`) demo zamienia na 422.
- Powiadomienia z bramek trafiają do jednego endpointu z surowym body; odpowiedź to `ResponseObject` dostawcy (Tpay: `TRUE`, Adyen:
  `[accepted]`). Powtórzone powiadomienie niczego nie zmienia.
- Do `PaymentRequest.AdditionalData` trafia własny numer zamówienia — Tpay i Adyen identyfikują nim płatność w powiadomieniu.
- Parametry stronicowania i sortowania sprawdza walidator (400 z nazwą pola), a request ma domyślną stronę i sortowanie, bo bez nich
  rozszerzenia zapytań zwracają całą tabelę w nieokreślonej kolejności.
- Wartości dla klienta (kwota w mailu) są formatowane niezależnie od kultury serwera.

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
