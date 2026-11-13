# Przykładowy serwis i testy integracyjne

!!! info "Linia 3.0 (faza testowa)"
    Przykład jest w repozytorium od linii 3.0 (`master`) i korzysta z bibliotek MediatR na **ForkForward.Mediator**
    (zob. [Aktualizacja do 3.0](../Migration/v3.md)). Nie jest paczką NuGet — nie trafia na nuget.org.

W `samples/` jest mały sklep zbudowany tak, jak budujemy prawdziwy serwis: trzy projekty — kontrakt, logika i host HTTP — które
używają **wszystkich 26 bibliotek naraz** razem z [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator)
i [ForkForward.Mapper](https://www.nuget.org/packages/ForkForward.Mapper). Ma katalog produktów, zamówienia, płatności przez dziewięć
bramek, powiadomienia z bramek i mail z potwierdzeniem. Działa bez kont i bez sieci: bramki płatności są podstawione, poczta trafia na
konsolę, a baza to SQLite w pamięci.

`tests/TailoredApps.Shared.IntegrationTests` uruchamia serwis w pamięci (`WebApplicationFactory`) i sprawdza go przez HTTP.
CI uruchamia te testy razem z resztą rozwiązania, więc zmiana w bibliotece, która psuje współpracę bibliotek, zatrzymuje PR.

---

## Uruchomienie

```bash
dotnet run --project samples/TailoredApps.Shared.Demo.WebApi -f net10.0
```

Profil `http` z `Properties/launchSettings.json` nasłuchuje na `http://localhost:5080` w środowisku Development. Dla `net11.0` przykład
jest kompilowany z runtime async, tak jak biblioteki ([Platformy docelowe](../Platform/index.md#runtime-async-net-11)).

Testy integracyjne:

```bash
dotnet test tests/TailoredApps.Shared.IntegrationTests/TailoredApps.Shared.IntegrationTests.csproj -f net10.0
```

---

## Trzy projekty

| Projekt | Co zawiera | Od czego zależy |
|---|---|---|
| `TailoredApps.Shared.Demo.DataModel` | Kontrakt. Dla każdej funkcji (`Catalog`, `Orders`): `Messages/Commands`, `Messages/Queries`, `Messages/Notifications`, `Responses/…`, `Model` | kontrakty mediatora, `MediatR.PagedRequest`, `MediatR.Caching`, `Payments` — bez EF Core i bez handlerów |
| `TailoredApps.Shared.Demo.Services` | Logika. Dla każdej funkcji: `Entities`, `Handlers` (każdy z interfejsem w `Interfaces/Handlers`), `Infrastructure` (repozytoria na unit of work, za `Interfaces/Infrastructure`), `Validation`, `Policies` (cache, retry, fallback), `Processing` (pre- i post-procesory, akcja i handler wyjątku, behavior strumienia), `Exceptions`, mapowanie EF (`IModelBuilder`) i profil mappera. W katalogu głównym `ShopDataContext` | `DataModel`, EF Core, biblioteki — nic z ASP.NET Core |
| `TailoredApps.Shared.Demo.WebApi` | Host. `Program` (buduje, zasila bazę, uruchamia) i `Startup` (`ConfigureServices`, `Configure`), `Controllers`, `Endpoints` (minimal API), `Authentication`, `ExceptionHandling` | oba powyższe i każda biblioteka |

Żądanie idzie w jedną stronę: **kontroler** zamienia HTTP na komunikat i go wysyła; **pipeline mediatora** waliduje, cache'uje, ponawia;
**handler** decyduje; **repozytorium** czyta i zapisuje przez unit of work i mapuje encje na model; **filtr transakcji** zatwierdza albo
wycofuje; **provider wyjątków** nadaje kształt błędowi. Żadna warstwa nie omija następnej.

---

## Gdzie jest która biblioteka

| Biblioteka | W przykładzie | Jak to zobaczyć |
|---|---|---|
| [WebApi](../Libraries/WebApi/index.md) | `AddTailoredWebApiDefaults()` i `MapTailoredWebApiDefaults()` w `Program.BuildApp` | `GET /health`, `GET /alive` |
| [DateTime](../Libraries/DateTime/index.md) | `AddDateTimeProvider()`; numer zamówienia, `CreatedAt`, `PaidAt` | testy podmieniają `TimeProvider` i sprawdzają czasy co do ticka |
| [ExceptionHandling](../Libraries/ExceptionHandling/index.md) | `ShopExceptionHandlingProvider`: `ShopException` odpowiada własnym statusem i komunikatem, reszta domyślnie | `GET /api/products/999` (404), duplikat produktu (409) |
| [EntityFramework](../Libraries/EntityFramework/index.md), [UnitOfWork.WebApiCore](../Libraries/EntityFramework/UnitOfWork.WebApiCore.md) | `ShopDataContext` z jednym `IModelBuilder` na funkcję, repozytoria na `IUnitOfWork<IShopDataContext>`, filtr transakcji | `POST /api/orders` z nieistniejącym produktem: 400 i nic nie zostaje w bazie |
| [UnitOfWork.MinimalApi](../Libraries/EntityFramework/UnitOfWork.MinimalApi.md), [ExceptionHandling.MinimalApi](../Libraries/ExceptionHandling/MinimalApi.md) | `Endpoints/ProductEndpoints.cs`: grupa z `WithExceptionHandling()`, `WithUnitOfWorkTransaction()` i `WithValidation<AddProduct>()`, wysyłająca te same komunikaty | `POST /api/minimal/products`, `GET /api/minimal/products/{name}` |
| [Querying](../Libraries/Querying/index.md), [MediatR.PagedRequest](../Libraries/MediatR/PagedRequest.md) | `GetProducts` z `ProductFilter`; w `ProductRepository`: `ApplySorting` z listą dozwolonych pól, `ProjectTo` mappera, `PagingAsync` | `GET /api/products?page=1&count=3&sortField=Price&sortDir=Desc&filter.category=tea` |
| [MediatR](../Libraries/MediatR/index.md), [MediatR.Caching](../Libraries/MediatR/Caching.md) | pięć behaviorów wokół każdego requestu; `GetProduct` (polityka cache), `GetRecommendations` (retry, fallback) | `GET /api/products/1`, `GET /api/products/0`, `GET /api/products/1/recommendations` |
| [Payments](../Libraries/Payments/index.md) + 9 providerów | `AddPayments().Register…Provider()`, `CheckoutOrderCommandHandler`, `ReceivePaymentNotificationCommandHandler` | `POST /api/orders/{number}/checkout`, `POST /api/webhooks/{provider}`, `GET /api/payments/providers` |
| [Email](../Libraries/Email/index.md), [Email.Models](../Libraries/Email/Models.md), [MediatR.Email](../Libraries/MediatR/Email.md) | `SendOrderConfirmation`: `SendMail` z szablonem `EmailTemplates/order-confirmation.html`, provider konsolowy | wiadomość na konsoli po opłaceniu zamówienia |
| [Email.Office365](../Libraries/Email/Office365.md) | `GET /api/inbox` — tylko z `Demo:Office365Inbox` i aplikacją Azure AD | bez konfiguracji: 501 |
| [MediatR.ML](../Libraries/MediatR/ML.md) | `POST /api/images/classify` — tylko z `ImageClassification:ModelFilePath` | bez modelu: 501 |

---

## Co pokazuje z mediatora i mappera

| Możliwość | W przykładzie | Jak rejestrowana |
|---|---|---|
| Request z odpowiedzią | każde zapytanie i komenda, np. `GetOrder` → `GetOrderResponse` | skanowanie assembly Services |
| Request bez odpowiedzi | `CancelOrder : IRequest`, handler zwraca `Task` | skanowanie |
| Powiadomienie z kilkoma handlerami | `OrderPaid` → `SendOrderConfirmation` (mail) i `RecordOrderPaid` (synchroniczny `NotificationHandler<T>`) | skanowanie; `ForeachAwaitPublisher` |
| Request strumieniowy | `ExportProducts : IStreamRequest<Product>`; kontroler zwraca `sender.CreateStream(...)` | skanowanie |
| Behavior strumienia | `CountExportedProducts` | `cfg.AddStreamBehavior<…>()` |
| Pre-procesor | `NormalizeCustomerEmail` (obcina spacje i zamienia na małe litery przed walidacją) | `cfg.AddRequestPreProcessor<…>()` — skanowanie nie znajduje procesorów |
| Post-procesor | `CountStartedPayments` (po udanym rozpoczęciu płatności) | `cfg.AddRequestPostProcessor<…>()` |
| Akcja wyjątku | `CountGatewayOutages`: tylko obserwuje, wyjątek i tak staje się odpowiedzią 503 | skanowanie |
| Handler wyjątku | `TreatMissingOrderAsCancelled`: zastępuje wyjątek odpowiedzią, więc anulowanie jest idempotentne | skanowanie |
| Behaviory bibliotek | Logging → Validation → Caching → Fallback → Retry, wewnątrz procesorów | `PipelineRegistration`, po `AddMediator` |
| Mapowanie przez konwencję | `Entities.Order` → `Model.Order`: status (enum) staje się nazwą, pozycje kolekcją, `ProductName` jest spłaszczane z `Product.Name` | `AddAutoMapper(assembly)`: profile z assembly Services |
| Jawny składnik | `LineTotal` liczony z `UnitPrice * Quantity`; `Id` pomijane, gdy komenda staje się encją | profile |
| Projekcja | `ProjectTo<Model.Product>()` w liście stronicowanej i w eksporcie: SQL wybiera tylko kolumny modelu | `IMapper.ConfigurationProvider` |
| Sprawdzenie konfiguracji | `AssertConfigurationIsValid()` przy starcie i w teście: zapomniany składnik wychodzi tam, a nie przy pierwszym żądaniu | `Program.PrepareDatabase` |

Kolejność dla requestu, od zewnątrz: handlery i akcje wyjątków → pre-procesory → post-procesory → pięć behaviorów bibliotek → handler.
Powiadomienia i strumienie nie przechodzą przez behaviory requestów. Mapper jest używany tylko w przykładzie: żadna biblioteka od niego
nie zależy i nie jest on zgodny z NativeAOT (rejestrację dla NativeAOT pokazuje `tests/TailoredApps.Shared.AotSmoke`).

---

## Autoryzacja

Serwis przyjmuje tokeny JWT (bearer) z własnym wystawcą i odbiorcą, podpisane kluczem `Jwt:Key` (co najmniej 32 znaki, inaczej serwis
nie wystartuje). Zmiana katalogu wymaga roli `CatalogAdmin`; zamówienia są otwarte. Klucz jest w `appsettings.Development.json` — to
wartość zastępcza do pracy lokalnej; inne środowisko dostarcza własny jako sekret. Na wdrożeniu tokeny wystawia serwis autoryzacji
organizacji; wyłącznie w środowisku Development zastępuje go `POST /api/dev/token`.

---

## Przejście przez sklep

Wszystkie polecenia działają w Git Bash i w powłoce Linuksa lub macOS (`openssl` liczy podpis powiadomienia).

```bash
# Produkty: pierwsza strona, trzy najdroższe
curl "http://localhost:5080/api/products?count=3&sortField=Price&sortDir=Desc"

# Katalog herbat jako strumień
curl "http://localhost:5080/api/products/export?category=tea"

# Nowy produkt wymaga roli CatalogAdmin: bez tokenu 401
token=$(curl -s -X POST "http://localhost:5080/api/dev/token?name=anna&role=CatalogAdmin" | sed -E 's/.*"token":"([^"]+)".*/\1/')
curl -X POST http://localhost:5080/api/products -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
  -d '{"name":"Matcha","category":"tea","price":89}'
# {"product":{"id":8,"name":"Matcha","category":"tea","price":89}}

# Zamówienie: dwie zielone herbaty i czajnik
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" \
  -d '{"customerEmail":"Anna@Demo.Example","productIds":[1,1,6]}'
# {"order":{"number":"ORD-20261112-cbef5f49","customerEmail":"anna@demo.example","amount":178.8,"status":"New",
#   "lines":[{"productId":1,"productName":"Green tea","unitPrice":24.9,"quantity":2,"lineTotal":49.8},...]}}

# Płatność przez Revolut: adres strony płatności i identyfikator płatności
curl -X POST http://localhost:5080/api/orders/ORD-20261112-cbef5f49/checkout -H "Content-Type: application/json" \
  -d '{"provider":"Revolut","channel":"card"}'
# {"number":"ORD-20261112-cbef5f49","paymentId":"REVOLUT-1","redirectUrl":"https://gateway.demo/pay/revolut/REVOLUT-1"}

# Powiadomienie o płatności, podpisane tak, jak podpisuje Revolut (sekret z appsettings.json)
body='{"event":"ORDER_COMPLETED","order_id":"REVOLUT-1"}'
ts=$(( $(date +%s) * 1000 ))
sig=$(printf 'v1.%s.%s' "$ts" "$body" | openssl dgst -sha256 -hmac 'demo-not-a-real-secret' | sed 's/^.*= //')
curl -X POST http://localhost:5080/api/webhooks/Revolut -H "Content-Type: application/json" \
  -H "Revolut-Request-Timestamp: $ts" -H "Revolut-Signature: v1=$sig" -d "$body"
# OK — a na konsoli aplikacji: recipent: anna@demo.example; topic: Order ORD-20261112-cbef5f49 is paid; ...

curl http://localhost:5080/api/orders/ORD-20261112-cbef5f49
# "status":"Paid","paidAt":"..."

# Opłaconego zamówienia nie da się anulować (409); nowe tak (204)
curl -i -X DELETE http://localhost:5080/api/orders/ORD-20261112-cbef5f49
```

Znacznik czasu powiadomienia musi być aktualny: Revolut odrzuca podpisy starsze niż 300 sekund (`WebhookToleranceSeconds`).

---

## Co jest podstawione

- **Bramki płatności** — `Payments/FakeGateway.cs` (projekt WebApi) jest głównym handlerem nazwanych klientów HTTP providerów
  (`AddHttpClient(nazwa).ConfigurePrimaryHttpMessageHandler(...)`) i odpowiada tak jak każda z bramek. Pierwszy segment ścieżki wskazuje
  bramkę: wartości `ServiceUrl` w `appsettings.json` prowadzą do `https://gateway.demo/<bramka>`. Stripe działa przez własne SDK, więc
  przykład rejestruje `IStripeClient` korzystający z tego samego handlera. Biblioteki providerów działają bez zmian — podmieniony jest
  tylko transport.
- **Awaria i odmowa bramki** — `FakeGatewayState.Unavailable` sprawia, że każda bramka odpowiada 503, a `FakeGatewayState.Refusing` 400;
  testy sprawdzają, że rozpoczęcie płatności odpowiada wtedy 503 albo 422, a zamówienie zostaje bez zmian.
- **Prawdziwe bramki** — ustaw `Demo:UseFakeGateways` na `false` i podaj dane sandboxu w konfiguracji (najlepiej w user secrets).
- **Podpisy powiadomień** są sprawdzane naprawdę, kluczami zastępczymi z `appsettings.json`.
- **Poczta** — provider konsolowy biblioteki Email; testy podmieniają go na provider zapisujący wiadomości.
- **Serwis autoryzacji** — `POST /api/dev/token`, mapowany tylko w środowisku Development.
- **Baza** — SQLite w pamięci na jednym otwartym połączeniu, zasilana przy starcie. SQLite nie ma typu `decimal`, więc kwoty są
  przechowywane w groszach, a czasy w tickach UTC — dokładnie i tak, że da się po nich sortować w SQL.

---

## Testy integracyjne

| Klasa | Co sprawdza |
|---|---|
| `WebApiDefaultsTests` | `/health` i `/alive` |
| `CatalogTests` | stronicowanie, sortowanie, filtr, walidacja parametrów (400 z nazwą pola; jeden wpis Information bez wartości pól w logu, żadnego Error), cache (drugi odczyt nie uruchamia handlera), 404, retry i fallback |
| `OrdersTests` | zapis i commit przez filtr, rollback po odrzuconym żądaniu, 404, rozpoczęcie płatności we wszystkich 9 bramkach (z żądaniem HTTP do bramki), awaria bramki (503) i odmowa (422) — także Stripe — przy niezmienionym zamówieniu |
| `WebhookTests` | podpisane powiadomienie → zamówienie opłacone i jeden mail z szablonu (także przy powtórzonym powiadomieniu), odrzucona płatność, zły podpis i stary znacznik czasu → 400 |
| `MinimalApiTests` | endpointy minimal API: commit bez `SaveChanges` w endpoincie, 400 z polami walidatora, 409 dla duplikatu, 404 z providera |
| `MediatorFeaturesTests` | 401 / 403 / token podpisany innym kluczem; strumień i jego behavior; konfiguracja mappera, spłaszczanie i składnik liczony; pre-procesor; post-procesor i akcja wyjątku; request bez odpowiedzi i handler wyjątku; oba handlery powiadomienia, także gdy poczty nie da się wysłać; brak klucza podpisu → serwis nie startuje |
| `ApplicationTests` | providerzy i kanały, 501 dla ML i Office365 bez konfiguracji |
| `RuntimeAsyncTests` | jak skompilowano metody asynchroniczne trzech assembly przykładu i bibliotek |

Testy zmieniają w serwisie dwie rzeczy: zegar (`TimeProvider`, zatrzymany w ustalonej chwili) i provider poczty. Cała reszta to własna
konfiguracja serwisu.

---

## Wnioski dla własnego serwisu

- Kontrolery wysyłają komunikat i zwracają odpowiedź; nie mają logiki, niczego nie łapią i nigdy nie zapisują.
- Nikt nie woła `SaveChanges`, żeby zakończyć żądanie: filtr transakcji zatwierdza po udanej akcji i wycofuje po wyniku 4xx/5xx albo
  wyjątku. Repozytorium zapisuje wewnątrz transakcji tylko po to, by poznać wygenerowany klucz.
- Serwis rzuca własne typy wyjątków dla tego, czego świadomie odmawia; provider wyjątków zamienia je na status i komunikat, a wszystko
  inne na 500 z ogólnym tekstem. Błąd transportu nigdy nie trafia do klienta.
- Handler rozpoczęcia płatności zamienia `HttpRequestException` (w tym `PaymentGatewayException`) na jeden wyjątek „bramka niedostępna"
  (503): ten jeden blok obejmuje wszystkich dziewięciu providerów, także Stripe. Odmowa (`Rejected`) to 422.
- Powiadomienia z bramek trafiają na jeden endpoint z surową treścią; odpowiedzią jest `ResponseObject` providera (Tpay: `TRUE`, Adyen:
  `[accepted]`). Powtórzone powiadomienie niczego nie zmienia.
- Handler powiadomienia, który rozmawia z czymś na zewnątrz (poczta), sam łapie swój błąd: przy domyślnym publisherze wyjątek zatrzymuje
  pozostałe handlery i przerywa kod, który opublikował powiadomienie.
- Pre- i post-procesory rejestruje się po nazwie; skanowanie ich nie znajduje.
- Walidator sprawdza parametry stronicowania i sortowania (400 z nazwą pola), a request ma domyślną stronę i sortowanie, bo bez nich
  rozszerzenia zapytań zwracają całą tabelę w przypadkowej kolejności.
- Żądania są walidowane w jednym miejscu (FluentValidation w pipeline); niejawne „required" MVC dla nienullowalnych stringów jest wyłączone.

---

## 🤖 AI Agent Prompt

````text
Użyj przykładowego serwisu TailoredApps (samples/TailoredApps.Shared.Demo.DataModel, .Services, .WebApi w repozytorium SharedComponents)
jako wzorca Web API zbudowanego na bibliotekach TailoredApps.Shared.*, ForkForward.Mediator i ForkForward.Mapper.

Struktura:
- DataModel: komunikaty (Commands, Queries, Notifications), odpowiedzi i modele dla każdej funkcji. Bez EF Core, bez handlerów.
- Services: encje, kontekst danych, repozytoria na IUnitOfWork<IMyDataContext>, handlery, walidatory, polityki, profile mappera.
  Bez ASP.NET Core.
- WebApi: Program (BuildApp, przygotowanie bazy), Startup (ConfigureServices, Configure), kontrolery, provider wyjątków.

Rejestracja (Startup.ConfigureServices, w tej kolejności):
- builder.AddTailoredWebApiDefaults() w Program; services.AddDateTimeProvider();
- services.AddDbContext<MyDataContext>(...); services.AddUnitOfWorkForWebApi<IMyDataContext, MyDataContext>();
- services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, MyExceptionHandlingProvider>();
- services.AddControllers(o => { o.Filters.AddUnitOfWorkTransactionAttribute(); o.Filters.AddExceptionHandlingFilterAttribute(); });
- services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...); services.AddAuthorization();
- repozytoria: services.AddTransient<IMyRepository, MyRepository>();
- services.AddAutoMapper(servicesAssembly);
- services.AddMediator(cfg => { cfg.RegisterServicesFromAssembly(servicesAssembly);
    cfg.AddRequestPreProcessor<MyPreProcessor>(); cfg.AddRequestPostProcessor<MyPostProcessor>(); cfg.AddStreamBehavior<MyStreamBehavior>(); });
  var pipeline = new PipelineRegistration(services); pipeline.RegisterPipelineBehaviors(); pipeline.RegisterPipelineBehaviors(servicesAssembly);
- services.AddValidatorsFromAssemblyContaining<MyDataContext>();
- services.AddPayments().RegisterRevolutProvider()...; poczta: RegisterConsoleProvider() albo provider SMTP, plus IMailMessageBuilder.
- Configure: app.ConfigureExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
  potem app.MapTailoredWebApiDefaults() w Program.

Co jest skonfigurowane: endpointy zdrowia, OpenTelemetry, odporność klientów HTTP; jeden zegar TimeProvider; odpowiedzi błędów w JSON;
unit of work zatwierdzany albo wycofywany dla każdego żądania; pipeline mediatora (handlery i akcje wyjątków, procesory, potem
logowanie, walidacja, cache, fallback, retry); mapowanie encji na model i projekcja; providerzy płatności za IPaymentService;
poczta z szablonów; uwierzytelnianie JWT bearer.

Konfiguracja: WebApiDefaults, Jwt (Key jako sekret, co najmniej 32 znaki), Payments:Providers:<Provider>, Mail:Providers:<Provider>;
appsettings.json przykładu wymienia wszystkie klucze.

Zasady:
- Akcja kontrolera tylko wysyła komunikat przez ISender i zwraca odpowiedź; opisz ją atrybutami [ProducesResponseType].
- Nigdy nie wołaj SaveChanges, żeby zakończyć żądanie; rzuć własny wyjątek serwisu, aby odmówić i wycofać żądanie.
- Odmowy wyprowadzaj z jednego bazowego wyjątku niosącego status HTTP; w providerze wyjątków mapuj tylko tę bazę.
- Łap HttpRequestException wokół RegisterPayment i GetStatus i rzucaj wyjątek serwisu „bramka niedostępna" (503);
  na PaymentStatus == Rejected odpowiadaj 422.
- Przekazuj surową treść żądania do HandleWebhookAsync i odpowiadaj wartością PaymentResponse.ResponseObject; obsługa ma być idempotentna.
- Własny numer zamówienia aplikacji wstaw do PaymentRequest.AdditionalData.
- Pre- i post-procesory oraz behaviory strumieni rejestruj po nazwie; skanowanie rejestruje tylko handlery, handlery i akcje wyjątków.
- Handler powiadomienia, który może zawieść z przyczyn zewnętrznych, łapie i loguje; nie pozwól mu przerwać publikacji.
- Mapuj profilami i ProjectTo; przy starcie wywołaj AssertConfigurationIsValid(). Nie używaj mappera w aplikacji NativeAOT.
- Waliduj parametry stronicowania i sortowania przez FluentValidation i daj requestom stronicowanym domyślną stronę i sortowanie.
- Używaj `using ForkForward.Mediator;` / AddMediator i `using ForkForward.Mapper;` / AddAutoMapper, nigdy paczek MediatR ani AutoMapper.
- Nigdy nie wstawiaj klucza podpisu do appsettings.json; przy jego braku serwis ma nie wystartować.
````
