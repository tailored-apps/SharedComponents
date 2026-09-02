# Przegląd bezpieczeństwa — wrzesień 2026

Ta strona dokumentuje przegląd bezpieczeństwa bibliotek współdzielonych wykonany 2 września 2026,
poprawki, które z nim trafiły do repozytorium, oraz punkty wymagające decyzji człowieka.

## Zakres i metoda

- Wszystkie biblioteki w `src/` (25 projektów, ok. 12 000 linii C#), projekty testowe,
  workflow GitHub Actions i skrypty PowerShell.
- Statyczny przegląd źródeł w pięciu przebiegach (rdzeń płatności i providerzy, EF/Querying,
  MediatR/Email/ML, WebApi/ExceptionHandling, CI); każde znalezisko zweryfikowane w kodzie
  przed napisaniem poprawki.
- `dotnet list package --vulnerable --include-transitive`: **brak podatnych pakietów NuGet**
  (bezpośrednich i przechodnich). `System.Linq.Dynamic.Core` jest przypięty powyżej poprawki CVE-2024-51417.
- Każda poprawka ma testy jednostkowe; cała solucja (build + ponad 900 testów) była zielona przed commitem.

## Znaleziska i status

| # | Waga | Obszar | Znalezisko | Status |
|---|------|--------|------------|--------|
| 1 | Krytyczne | Payments (testy) | Produkcyjny sekret sklepu CashBill wraz z pasującymi podpisami był zakomitowany w pliku testowym (nieużywane dane). | **Usunięty** ze źródeł. Rotacja i wyczyszczenie historii wymagają właściciela — zobacz [Zadania dla właściciela](#zadania-dla-wlasciciela-repozytorium). |
| 2 | Wysokie | ExceptionHandling | Domyślny provider zwracał komunikat najgłębszego wyjątku (błędy SQL, hosty, ścieżki) anonimowym klientom jako HTTP 400 w każdym środowisku. | **Naprawione**: 500 + ogólny komunikat; szczegóły tylko w Development lub po jawnym włączeniu. |
| 3 | Wysokie | Payments (wszyscy providerzy) | Weryfikacja podpisu przy braku sekretu degradowała się do hasha bez klucza — każdy mógł sfałszować „poprawne” powiadomienie. | **Naprawione**: fail-closed w 9 providerach (w tym Stripe). |
| 4 | Wysokie | Payments (CashBill) | Komunikat błędu webhooka zwracał oczekiwany podpis (wyrocznia do fałszowania); starsza ścieżka `TransactionStatusChange` nie weryfikowała podpisu. | **Naprawione**. |
| 5 | Wysokie | Payments (Revolut) | Podpisywany ładunek używał `v1:` zamiast `v1.`; brak kontroli znacznika czasu — przechwycony webhook można było odtwarzać bez końca. | **Naprawione**: właściwy format, tolerancja 300 s (konfigurowalna), podpisy rotacyjne. |
| 6 | Wysokie | EntityFramework | `SortField` z żądania trafiał wprost do Dynamic LINQ (nawigacje, wiele kolumn, ujawnianie schematu); brak górnego limitu rozmiaru strony. | **Naprawione**: walidacja identyfikatora, przeciążenie z allow-listą, limity stron i kontrola przepełnienia. |
| 7 | Wysokie | MediatR | Domyślny klucz cache używał `ToString()` właściwości — żądania różniące się tylko zawartością kolekcji dzieliły wpis (dane innych użytkowników). Scope logowania serializował całe żądania (sekrety, obrazy). | **Naprawione**: klucze z hasha treści; scope zawiera tylko nazwę typu i correlation id. |
| 8 | Wysokie | Email | Zmienne szablonu wstawiane dosłownie do HTML (linki phishingowe od zaufanego nadawcy); lista adresów po przecinku dodawała odbiorców; TLS SMTP domyślnie wyłączony. | **Naprawione**: kodowanie HTML domyślnie (`{{{raw}}}` jako wyjątek), jeden adresat, TLS włączony. |
| 9 | Wysokie | MediatR.ML | Komenda treningu czytała dowolny katalog i pisała pod dowolną ścieżkę z żądania; klasyfikacja przyjmowała dowolne, niezwalidowane bajty. | **Naprawione**: ścieżki ograniczone do `TrainingRoot`/`ModelsRoot`; walidacja rozmiaru i sygnatury. |
| 10 | Średnie | WebApi | Standardowy handler odporności ponawiał POST/PATCH dla każdego HttpClienta — duplikaty płatności/zamówień po timeoutach. | **Naprawione**: ponawianie metod nieidempotentnych domyślnie wyłączone. |
| 11 | Średnie | Payments | Porównanie podpisów w czasie zależnym od danych; nagłówki wrażliwe na wielkość liter (nie działa pod HTTP/2); nieescapowane identyfikatory w URL bramek; wyniki webhooków bez identyfikatora płatności. | **Naprawione**: `WebhookSignature`, słowniki case-insensitive, `PaymentIdentifier`, wypełnione identyfikatory. |
| 12 | Średnie | Payments (Przelewy24) | Podpisy notyfikacji/verify liczone z niewłaściwych zestawów pól (każde prawdziwe powiadomienie odrzucane). | **Naprawione** zgodnie z udokumentowaną kolejnością pól. |
| 13 | Średnie | EF UnitOfWork | Filtr transakcji commitował, gdy akcja zwróciła 4xx/5xx; obsługa błędu commitu mogła zostawić otwartą transakcję i ukryć pierwotną przyczynę. | **Naprawione**. |
| 14 | Średnie | Email.Office365 | Załączniki zwracane w kodowaniu transferowym i kluczowane typem MIME (awaria przy dwóch załącznikach tego samego typu). | **Naprawione**. |
| 15 | Średnie | CI/CD | Tokeny z prawem zapisu i zachowanymi poświadczeniami, akcje przypięte do ruchomych tagów, nazwa tagu interpolowana do skryptu, publikacja NuGet bez testów i z ukrywaniem błędów, niezaprzypięte instalacje pip. | **Naprawione**: minimalne uprawnienia, przypięcie do SHA + Dependabot, przekazywanie przez env, testy przed publikacją, `--skip-duplicate`, przypięty mkdocs-material. |
| 16 | Średnie | Payments (Tpay, Adyen) | Weryfikacja webhooka nie odpowiada protokołowi dostawcy (Tpay: formularz + md5sum + JWS; Adyen: HMAC z pól rozdzielonych dwukropkami w `additionalData`). Fail-closed, więc niewykorzystywalne, ale niefunkcjonalne. | **Udokumentowane**; utwardzone (fail-closed, stały czas). Przepisanie w zadaniach do wykonania. |

## Co się zmieniło, per biblioteka

### Payments
- Nowe `TailoredApps.Shared.Payments.Security.WebhookSignature` (`FixedTimeEquals`,
  `FixedTimeEqualsIgnoreCase`, `IsSecretConfigured`) i `PaymentIdentifier` (`IsSafe`, `EnsureSafe`).
- `PaymentWebhookRequest.Headers`/`Query` są case-insensitive niezależnie od przypisanego słownika;
  `TransactionStatusChangePayload.QueryParameters` nigdy nie jest `null`.
- Wszyscy providerzy: fail-closed przy pustym sekrecie/podpisie, porównanie w stałym czasie,
  `PaymentUniqueId` z payloadu, zdarzenia `Processing` jako `Ignore`, walidacja identyfikatorów
  przed wstawieniem do URL, zwalnianie `JsonDocument`.
- CashBill: starsza ścieżka weryfikuje podpis MD5; komunikaty nie zawierają oczekiwanej wartości;
  brak sekretu → „Signature verification is not configured.”.
- Revolut: `v1.{timestamp}.{body}`, `WebhookToleranceSeconds` (domyślnie 300), podpisy rotacyjne.
- Przelewy24: udokumentowane zestawy pól podpisu z JSON bez escapowania unicode; waluta normalizowana raz.
- PayU: obsługa SHA-1, nieznane algorytmy odrzucane. PayNow: stały `Idempotency-Key`, status `EXPIRED`.
- Stripe: brak `WebhookSecret`/nagłówka → `Fail`; zdarzenia płatności asynchronicznych z identyfikatorami.
- Adyen: brak pola `success` traktowany jako niepowodzenie; `merchantReference`/`pspReference` w wyniku.
- HotPay: podpisywana nazwa usługi jest tą wysyłaną.

### ExceptionHandling
- `DefaultExceptionHandlingProvider`: walidacja → 400; reszta → 500 z komunikatem
  `"An unexpected error occurred."`; konstruktory `()`, `(IHostEnvironment)`, `(bool)`.
- Middleware używa `GetRequiredService`, nie pisze po rozpoczęciu odpowiedzi, ogranicza kod do 400–599.
  `ExceptionOccuredResult` honoruje kod z modelu. Usunięta zdublowana rejestracja DI.
  Dokumentacja i prompt agenta przepisane pod rzeczywiste API.

### WebApi
- `HttpClientSettings.RetryUnsafeHttpMethods` (domyślnie `false`) → `Retry.DisableForUnsafeHttpMethods()`.

### MediatR
- `ICachePolicy.GetCacheKey` → `{Typ.FullName}:{SHA-256(JSON)}` przez `CacheKeyGenerator`.
- `LoggingBehavior` nie serializuje żądania; stack trace nie jest doklejany do szablonu komunikatu.
  `CachingBehavior` używa strukturalnych szablonów logów.

### Email
- `TokenReplacingMailMessageBuilder`: podmiana regexem w jednym przebiegu, `{{token}}` kodowane HTML,
  `{{{token}}}` surowe, opcja `HtmlEncodeVariables`, argumenty odporne na `null`.
- `DefaultMessageBuilder`: jeden przebieg, najdłuższy klucz pierwszy, kodowanie domyślne (konstruktor `(bool)`).
- `SmtpEmailProvider`: dokładnie jeden adresat (`FormatException` dla list), `CatchAll` wymagany
  przy `IsProd = false`, `EnableSsl` domyślnie `true`, poprawny Message-ID.
- `Office365EmailProvider`: załączniki dekodowane i kluczowane nazwą pliku.

### MediatR.ML
- `ImageClassificationOptions.TrainingRoot`, `ModelsRoot`, `MaxImageBytes` (domyślnie 10 MB).
- `TrainImageClassificationModelCommandHandler` ogranicza obie ścieżki (`ArgumentException` poza
  korzeniami, `InvalidOperationException` gdy korzenie nie są skonfigurowane).
- `ClassifyImageCommandHandler` sprawdza obecność, rozmiar i sygnaturę JPEG/PNG.
  Wykrywanie JPEG akceptuje każdy nagłówek `FF D8 FF`.

### EntityFramework / Querying
- `QuerySortingExtensions`: pole sortowania musi być prostym identyfikatorem; przeciążenia
  `ApplySorting(..., allowedSortFields)`; błędy parsowania Dynamic LINQ przepakowane w `ArgumentException`
  bez szczegółów typu.
- `PagingQuery<T>`: `MaxPageSize` (domyślnie 1000, statycznie i per instancja), `Page >= 1`,
  `1 <= Count <= max`, bezpieczne obliczenie skip.
- `TransactionFilterAttribute`: rollback przy wynikach 4xx/5xx; `AggregateException` gdy commit i rollback
  zawiodą. `UnitOfWork`: commit/rollback zwalniają transakcję w `finally`.

### CI/CD
- Wszystkie workflow: domyślnie `permissions: contents: read`, `persist-credentials: false`, akcje
  przypięte do SHA commitów (z komentarzem wersji), `.github/dependabot.yml` dla `github-actions`.
- `ci.yml`: build/test oddzielony od joba publikującego `gh-pages` (tylko ten ma `contents: write`).
- `release.yml`: `dotnet test` przed publikacją, jeden push oparty na `find` z `--skip-duplicate`,
  klucz API przez zmienną środowiskową, `environment: nuget` (dodaj wymaganych recenzentów w ustawieniach repozytorium).
- `release-notes.yml`: nazwa tagu przez `env`, bez interpolacji do skryptu.
- `bump-deps.yml`: pełny przebieg testów przed utworzeniem PR.

## Zadania dla właściciela repozytorium

1. **Zrotuj sekret CashBill** sklepu wskazanego w usuniętych danych testowych — w panelu sprzedawcy
   CashBill. Wartość jest w historii gita od commitu `ed838d6`; rotacja to jedyne pewne rozwiązanie.
   Jeśli repozytorium jest lub było publiczne, wyczyść też historię (`git filter-repo`) i wykonaj
   force-push — to operacja destrukcyjna, celowo tutaj niewykonana.
2. Sekret sandboxowy CashBill w `tests/TailoredApps.Shared.Payments.Tests/appsettings.json` to
   poświadczenie środowiska testowego; przenieś go do user-secrets/sekretów CI, jeśli sklep testowy ma znaczenie.
3. W ustawieniach repozytorium dodaj wymaganych recenzentów do nowego środowiska `nuget`, aby push tagu
   nie publikował pakietów bez akceptacji, i ustaw checki *Docs guard* oraz *Build & Test PR* jako wymagane na `master`.
4. Skonfiguruj `ImageClassification:TrainingRoot` / `ModelsRoot` w hostach używających komendy treningu
   i przekazuj `allowedSortFields` do `ApplySorting` w endpointach listujących wrażliwe encje.
5. Przejrzyj konsumentów zależnych od starych zachowań: 400 z komunikatem wyjątku (teraz 500 ogólne),
   surowy HTML w zmiennych maili (teraz kodowany; użyj `{{{token}}}`), SMTP bez TLS (`EnableSsl` teraz `true`),
   listy odbiorców w `SendMail`, ponawianie POST przez domyślną odporność WebApi.

## Do wykonania (poza tą zmianą)

- Tpay: implementacja prawdziwego protokołu notyfikacji (formularz, `md5sum`, `X-JWS-Signature`
  z certyfikatem głównym Tpay). Adyen: HMAC z udokumentowanych pól rozdzielonych dwukropkami z
  `additionalData.hmacSignature`; `/sessions` zwraca `sessionData`, nie URL przekierowania.
- Błędy transportu (5xx, timeouty) nadal są raportowane przez część providerów jako
  `PaymentStatusEnum.Rejected`; dodaj wynik `Unknown`, by awaria nie była mylona z odmową.
- `CachingBehavior` nie odróżnia braku wpisu od zapisanej wartości domyślnej dla typów wartościowych;
  zmień `ICache.GetAsync` tak, by zwracał flagę znalezienia.
- Konwersja kwot na jednostki mniejsze obcina i zakłada dwa miejsca dziesiętne; dodaj wspólny helper ISO-4217.
- PayU tworzy surowy `HttpClient` per zamówienie; użyj nazwanego klienta z `AllowAutoRedirect = false`.
  Tokeny OAuth (PayU, Tpay) są pobierane przy każdym wywołaniu.
- EF: `SET TRANSACTION ISOLATION LEVEL` na poziomie sesji działa tylko na SQL Server i jest zbędne;
  wielokrotne `AddUnitOfWork` krzyżuje konteksty; kolektor audytu pomija encje proxy;
  `HasDefaultValue(DateTime.UtcNow)` utrwala stałą w migracjach.
- Higiena pakietów: `Nullable` wyłączone w większości projektów, brak `PackageReadmeFile` w większości
  pakietów, wersje `x.*` bez pliku lock, `SourceRevisionId` z `UtcNow`, martwe warunki TFM.
- `PagedAndSortedRequest` duplikuje `PagedAndSortedQuery`; dodaj atrybuty `[Range]`.

## Weryfikacja poprawek lokalnie

```bash
dotnet test TailoredApps.Shared.sln
python -m mkdocs build --strict --config-file mkdocs.yml
python -m mkdocs build --strict --config-file mkdocs-en.yml
dotnet list TailoredApps.Shared.sln package --vulnerable --include-transitive
```
