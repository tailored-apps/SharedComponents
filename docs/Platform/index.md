# Platformy docelowe i runtime async

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Każda paczka `TailoredApps.Shared.*` jest budowana dla kilku platform docelowych jednocześnie (multi-targeting). NuGet sam wybiera asset pasujący do `TargetFramework` Twojej aplikacji — nie trzeba niczego konfigurować.

---

## Wspierane platformy docelowe

| Biblioteki | Platformy docelowe |
|---|---|
| `DateTime`, `Email.Models`, `MediatR.Caching`, `Querying` | `netstandard2.0`, `netstandard2.1` |
| `Email`, `Email.Office365`, `EntityFramework`, `EntityFramework.UnitOfWork.WebApiCore`, `ExceptionHandling`, `MediatR`, `MediatR.Email`, `MediatR.ML`, `MediatR.PagedRequest`, `Payments` i wszyscy providerzy płatności, `WebApi` | `net8.0`, `net9.0`, `net10.0`, `net11.0` |

Biblioteki `netstandard` działają na każdej z powyższych wersji .NET, w tym na .NET 11 — nie potrzebują osobnego assetu.

### Zależności dopasowane do platformy

Paczki powiązane z wersją runtime'u (`Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore*`, `System.Configuration.ConfigurationManager`) są referowane osobno dla każdej platformy docelowej: asset `net11.0` zależy od linii `11.x`, `net10.0` od `10.x` i tak dalej. Aplikacja na .NET 11 nie dostaje więc w zależnościach paczek z poprzedniej generacji.

!!! note "Wyjątek: WebApi na net11.0"
    W dniu wydania .NET 11 na nuget.org nie było jeszcze linii `11.x` paczek `Microsoft.Extensions.Http.Resilience` i `Microsoft.Extensions.ServiceDiscovery`, dlatego asset `net11.0` biblioteki `WebApi` korzysta tymczasowo z ich linii `10.x`.

---

## Runtime async (.NET 11)

W .NET 11 zawieszaniem i wznawianiem metod `async` może zająć się sam runtime. Kompilator C# nie generuje wtedy osobnego typu maszyny stanów dla każdej metody `async` — metoda zostaje zwykłą metodą oznaczoną flagą `MethodImplOptions.Async`. Celem jest mniejszy narzut wywołań asynchronicznych i mniejszy kod IL. Kod źródłowy i publiczne API pozostają bez zmian: sygnatury nadal zwracają `Task` / `ValueTask`.

Biblioteki z tego repozytorium korzystają z tego mechanizmu w assetach `net11.0`:

| Asset w paczce | Sposób kompilacji metod `async` |
|---|---|
| `net11.0` | **runtime async** |
| `net8.0`, `net9.0`, `net10.0` | maszyna stanów generowana przez kompilator (bez zmian) |
| `netstandard2.0`, `netstandard2.1` | maszyna stanów generowana przez kompilator (bez zmian) |

### Co to oznacza dla Twojej aplikacji

- **Nic nie musisz włączać.** Aplikacja na `net11.0` dostaje asset z runtime async automatycznie; aplikacje na `net8.0`–`net10.0` dostają dokładnie to, co wcześniej.
- **Twoja aplikacja nie musi sama używać runtime async.** Kod skompilowany klasycznie może swobodnie wywoływać metody bibliotek skompilowane jako runtime async i odwrotnie — testy tego repozytorium są kompilowane klasycznie i uruchamiane zarówno na `net10.0`, jak i na `net11.0`.
- **Nie potrzeba zmiennych środowiskowych** ani `<EnablePreviewFeatures>`.

!!! warning "Wymaga CoreCLR lub NativeAOT"
    Runtime async działa na CoreCLR i NativeAOT. Runtime Mono — używany m.in. przez **Blazor WebAssembly** — nie potrafi w .NET 11 wykonać takich metod. Jeżeli chcesz użyć którejś z bibliotek zawierających kod `async` po stronie klienta Blazor WebAssembly na `net11.0`, zbuduj ją z wyłączonym runtime async (patrz niżej). Biblioteki `netstandard` (`DateTime`, `Email.Models`, `MediatR.Caching`, `Querying`) nie są tym objęte.

!!! info "Status funkcji"
    W .NET 11 runtime async jest funkcją typu *opt-in*, którą dokumentacja Microsoftu wciąż opisuje jako preview: [What's new in the .NET 11 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime).

### Jak to jest włączone w repozytorium

Jedno miejsce: `src/Directory.Build.targets`. Plik dopisuje `runtime-async=on` do właściwości `Features` kompilatora wyłącznie dla platform zgodnych z `net11.0`:

```xml
<PropertyGroup Condition="'$(UseRuntimeAsync)' != 'false' and '$(TargetFramework)' != '' and $([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net11.0'))">
  <Features>$(Features);runtime-async=on</Features>
</PropertyGroup>
```

Warunek jest konieczny: ustawienie tej flagi dla `net10.0` kończy build błędami `SYSLIB5007` / `CA2252`, a na starszych platformach flaga jest po cichu ignorowana.

### Wyłączanie

| Zakres | Jak |
|---|---|
| Cały build | `dotnet build TailoredApps.Shared.slnx -p:UseRuntimeAsync=false` |
| Jedna biblioteka | `<UseRuntimeAsync>false</UseRuntimeAsync>` w jej pliku `.csproj` |

Po wyłączeniu asset `net11.0` jest kompilowany tak samo jak pozostałe — z maszynami stanów.

---

## Budowanie repozytorium

- Wymagany jest **.NET SDK 11.0.100 lub nowszy** — buduje wszystkie platformy docelowe od `net8.0` do `net11.0`.
- Testy uruchamiają się na `net10.0` i `net11.0`, więc lokalnie potrzebne są oba runtime'y.
- Workflowy CI instalują SDK `10.0.x` (runtime dla testów `net10.0`) oraz `11.0.x` (build).

```bash
dotnet build TailoredApps.Shared.slnx
dotnet test TailoredApps.Shared.slnx
```

### Nowa biblioteka

- Użyj `<TargetFrameworks>net8.0;net9.0;net10.0;net11.0</TargetFrameworks>`.
- Paczki powiązane z wersją runtime'u referuj warunkowo, osobno dla każdej platformy (`Condition="'$(TargetFramework)' == 'net11.0'"` → `Version="11.*"`).
- Nie ustawiaj `Features` w pliku `.csproj` — runtime async jest dziedziczony z `src/Directory.Build.targets`.
- Dodaj `net11.0` do `<TargetFrameworks>` projektu testów, aby asset `net11.0` był faktycznie testowany.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps Shared Components — platformy docelowe i runtime async — Instrukcja dla agenta AI

Pracujesz z paczkami TailoredApps.Shared.* albo z repozytorium SharedComponents.

### Rejestracja
- Brak. Wybór platformy docelowej nie wymaga żadnych wywołań w Program.cs — NuGet sam dobiera
  asset zgodny z TargetFramework aplikacji.

### Co jest skonfigurowane
- Platformy: net8.0, net9.0, net10.0, net11.0. Wyjątki tylko netstandard2.0/netstandard2.1:
  DateTime, Email.Models, MediatR.Caching, Querying.
- Assety net11.0 mają metody async skompilowane jako runtime async; pozostałe assety używają
  maszyn stanów generowanych przez kompilator. Publiczne API jest identyczne na każdej platformie.
- Zależności Microsoft.Extensions.* / EF Core są dopasowane do platformy (net11.0 -> 11.x).

### Konfiguracja
- W repozytorium: właściwość MSBuild UseRuntimeAsync (domyślnie włączona dla net11.0+),
  obsługiwana w src/Directory.Build.targets. Wyłączenie: -p:UseRuntimeAsync=false albo
  <UseRuntimeAsync>false</UseRuntimeAsync> w csproj biblioteki.
- W aplikacji konsumującej paczki: brak ustawień.

### Zasady
- Nie dodawaj <Features>runtime-async=on</Features> ani <EnablePreviewFeatures> do plików csproj
  bibliotek — przełącznik jest centralny i warunkowany platformą.
- Nie ustawiaj runtime-async dla net10.0 ani starszych platform: build kończy się błędami
  SYSLIB5007 / CA2252.
- Nie używaj assetów net11.0 bibliotek z kodem async w Blazor WebAssembly (Mono nie obsługuje
  runtime async w .NET 11) — zbuduj je z UseRuntimeAsync=false.
- Nowa biblioteka: TargetFrameworks net8.0;net9.0;net10.0;net11.0, zależności runtime'owe
  warunkowo per platforma, projekt testów z net10.0;net11.0.
- Do budowania repozytorium używaj .NET SDK 11.0.100 lub nowszego.
```
