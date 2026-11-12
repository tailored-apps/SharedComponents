# Contributing

Dziękujemy za zainteresowanie współtworzeniem **TailoredApps Shared Components**! Ten dokument opisuje proces contributingu oraz **obowiązkowe wymagania dokumentacyjne**.

---

## Zasady ogólne

1. **Fork → Feature Branch → PR** — nie commituj bezpośrednio na `master`
2. Nazwa brancha: `feature/<short-name>` lub `fix/<short-name>`
3. Jeden PR = jedna zmiana merytoryczna
4. Wszystkie testy muszą przechodzić przed otwarciem PR
5. Kod musi posiadać XML docs (summary) dla publicznych typów i metod
6. Jedna przestrzeń nazw na plik, deklarowana bez bloku (`namespace TailoredApps.Shared.Xxx;`). Styl wymusza `.editorconfig`; istniejący kod poprawisz poleceniem `dotnet format style TailoredApps.Shared.slnx --diagnostics IDE0161 --severity info`

---

## Środowisko lokalne

Wymagany jest **.NET SDK 11.0.100 lub nowszy** — buduje obie platformy docelowe (`net10.0` i `net11.0`). Testy uruchamiają się na `net10.0` i `net11.0`, więc potrzebne są oba runtime'y. Konwencje dotyczące platform docelowych i runtime async opisuje strona [Platformy docelowe i runtime async](Platform/index.md).

```bash
git clone https://github.com/tailored-apps/SharedComponents.git
cd SharedComponents

# .NET
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
export PATH="$PATH:/opt/homebrew/opt/dotnet/bin"
dotnet restore

# Dokumentacja
pip install mkdocs-material
mkdocs serve  # http://127.0.0.1:8000
```

### Solution i katalog wyjściowy

Repozytorium używa pliku solution w formacie XML — `TailoredApps.Shared.slnx` (otwiera go `dotnet` CLI oraz Visual Studio 2022 17.13 lub nowsze).

```bash
dotnet build TailoredApps.Shared.slnx
dotnet test TailoredApps.Shared.slnx
```

Plik `Directory.Build.props` w katalogu głównym włącza `UseArtifactsOutput`, więc projekty **nie** tworzą już własnych folderów `bin/` i `obj/`. Całość trafia do jednego, ignorowanego przez git katalogu `artifacts/`:

| Ścieżka | Zawartość |
|---------|-----------|
| `artifacts/bin/<Projekt>/<konfiguracja>_<tfm>/` | skompilowane biblioteki, np. `artifacts/bin/TailoredApps.Shared.WebApi/release_net10.0/` |
| `artifacts/obj/<Projekt>/` | pliki pośrednie |
| `artifacts/package/<konfiguracja>/` | paczki NuGet (`*.nupkg`) wszystkich bibliotek — stąd publikuje je workflow release |

Aby wyczyścić build, wystarczy usunąć katalog `artifacts/`. Nową bibliotekę dodaj do solution poleceniem `dotnet sln TailoredApps.Shared.slnx add <ścieżka do .csproj>` — ustawienia z `Directory.Build.props` zostaną zastosowane automatycznie: platformy docelowe, wersja języka, nullable, metadane paczki (autor, licencja MIT, adres projektu) i generowanie paczki NuGet. Wersje paczek są w `Directory.Packages.props`, a `nuget.config` ogranicza źródła do nuget.org. Plików `*.user` oraz folderów `.vs/`, `.idea/`, `.vscode/` nie commitujemy (są w `.gitignore`).

---

## 🔴 ŻELAZNA ZASADA DOKUMENTACJI

> **Każda nowa biblioteka w tym repo MUSI posiadać stronę dokumentacji w `docs/Libraries/`.**
>
> **PR bez dokumentacji = PR odrzucony.**

### Wymagania dla każdej strony dokumentacji

Każda strona `docs/Libraries/<NazwaBiblioteki>/index.md` musi zawierać:

1. **Header + badges** — nazwa biblioteki, badge NuGet i licencji
2. **Opis działania** — po **polsku** 🇵🇱 i po **angielsku** 🇬🇧
3. **Instalacja** — `dotnet add package ...`
4. **Rejestracja w DI** — przykład z `Program.cs`
5. **Przykład użycia** — realny, kompletny kod C# (nie toy example)
6. **API Reference** — tabela/lista głównych interfejsów i klas
7. **🤖 AI Agent Prompt** — gotowy prompt do wklejenia w kontekst agenta AI

### Aktualizacja nawigacji

Po dodaniu strony biblioteki zaktualizuj sekcję `nav:` w `mkdocs.yml` oraz tabelę bibliotek na `docs/index.md`.

### Dwa języki i README pakietu

- Serwis ma dwa drzewa: `docs/` (polskie) i `docs-en/` (angielskie). Każda strona istnieje w obu, pod tą samą ścieżką względną, i jest zarejestrowana
  w `mkdocs.yml` oraz `mkdocs-en.yml`; tabela bibliotek jest na `docs/index.md` i `docs-en/index.md`. Zmiana w bibliotece aktualizuje obie wersje.
- Każda biblioteka ma `README.md` obok swojego csproj: czym jest pakiet, instalacja, rejestracja, przykład użycia, konfiguracja i link do strony
  w tym serwisie. Plik jest pakowany do paczki NuGet automatycznie (`src/Directory.Build.props`) i to on jest widoczny na nuget.org — do csproj
  nie trzeba nic dodawać.
- Przykłady kodu muszą kompilować się z aktualnym kodem: nazwy typów, metod i kluczy konfiguracji sprawdzaj w źródłach, nie z pamięci.

### Aplikacja demo i testy integracyjne

[Aplikacja demo](Demo/index.md) (`samples/TailoredApps.Shared.Demo`) używa każdej biblioteki, a `tests/TailoredApps.Shared.IntegrationTests`
sprawdza ją przez HTTP. Nowa biblioteka trafia też do demo, razem z testem swojego scenariusza; zmiana, po której biblioteka jest używana
inaczej, aktualizuje demo. Demo działa bez sieci: zewnętrzna usługa jest podstawiana na poziomie transportu (handler HTTP), nigdy przez
zmianę biblioteki.

### Szablon strony biblioteki

```markdown
# TailoredApps.Shared.XXXXX

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.XXXXX)](https://www.nuget.org/packages/TailoredApps.Shared.XXXXX/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

## 🇵🇱 Opis

[Pełny opis po polsku...]

## 🇬🇧 Description

[Full description in English...]

## Instalacja

\`\`\`bash
dotnet add package TailoredApps.Shared.XXXXX
\`\`\`

## Rejestracja w DI

\`\`\`csharp
// Program.cs
builder.Services.AddXxx();
\`\`\`

## Przykład użycia

\`\`\`csharp
// ...
\`\`\`

## API Reference

| Typ | Opis |
|-----|------|
| `IXxx` | ... |

## 🤖 AI Agent Prompt

\`\`\`markdown
## TailoredApps.Shared.XXXXX — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.XXXXX w projekcie .NET.

### Rejestracja
...

### Użycie
...

### Zasady
- ...
\`\`\`
```

---

## Checklist przed otwarciem PR

- [ ] Kod kompiluje się bez błędów (`dotnet build`)
- [ ] Testy przechodzą (`dotnet test`)
- [ ] XML docs dodane do publicznych typów
- [ ] Strona dokumentacji w `docs/Libraries/`
- [ ] `mkdocs.yml` nav zaktualizowany
- [ ] Tabela na `docs/index.md` zaktualizowana
- [ ] `mkdocs build --strict` przechodzi bez błędów
- [ ] `dotnet build TailoredApps.Shared.slnx -c Release -p:ValidatePublicApi=true` przechodzi (brak zmian niekompatybilnych z 2.0.0)

---

## Zgodność publicznego API

Gałąź `master` to linia **3.0 w fazie testowej** (`3.0.0-preview.N`): zmiany niekompatybilne są dozwolone do wydania `3.0.0`, ale tylko
zamierzone. Gałąź `release/2.1` to linia poprawek 2.1.x, zgodna z `2.0.0`. Biblioteki `TailoredApps.Shared.MediatR*` są na `master` wyłączone
z porównania (przeszły z MediatR na ForkForward.Mediator); pozostałe są nadal porównywane, żeby zmiana łamiąca nie zdarzyła się przypadkiem.

Linia 2.x musi pozostać zgodna z `2.0.0`: kod skompilowany z dowolną wersją 2.x ma działać z każdą nowszą 2.x. Sprawdzenie
*Build & Test PR* porównuje publiczne API każdej biblioteki z paczką `2.0.0` z nuget.org i kończy się błędem przy zmianie
niekompatybilnej (usunięcie lub zmiana sygnatury publicznego typu albo składowej — `CP0001`, `CP0002`, …). Dodawanie API jest dozwolone.

```bash
# to samo lokalnie
dotnet build TailoredApps.Shared.slnx -c Release -p:ValidatePublicApi=true
```

- Wersję bazową ustawia `PublicApiBaselineVersion` w `src/Directory.Build.targets`.
- Nowa biblioteka: do pierwszego wydania dodaj w jej csproj `<SkipPublicApiValidation>true</SkipPublicApiValidation>`,
  a po wydaniu zastąp to przez `<PublicApiBaselineVersion>` z numerem pierwszej wersji, w której się ukazała.
- Zmiana niekompatybilna jest możliwa dopiero w kolejnej wersji głównej (3.0). Zamiast usuwać składową, oznacz ją `[Obsolete]` i dodaj nową.

---

## Wydawanie wersji (maintainerzy)

Wypchnięcie tagu `vX.Y.Z` (lub wersji przedpremierowej, np. `v2.0.0-preview.1`) uruchamia workflow *Create Release* (`.github/workflows/release.yml`): buduje wszystkie biblioteki z wersją wziętą z tagu, uruchamia testy i publikuje paczki z `artifacts/package/release` w nuget.org. Job kończy się błędem, jeśli którejkolwiek paczki nie udało się opublikować. Workflow *Release notes* tworzy GitHub Release na podstawie zmergowanych pull requestów.

Paczki są publikowane przez **Trusted Publishing w nuget.org** — repozytorium nie przechowuje klucza API NuGet. Job prosi o token OIDC z GitHuba, a nuget.org wymienia go na klucz API ważny godzinę. Wymaga to jednorazowej konfiguracji:

| Gdzie | Ustawienie |
|-------|------------|
| nuget.org → menu konta → **Trusted Publishing** → nowa polityka | Repository Owner `tailored-apps`, Repository `SharedComponents`, Workflow File `release.yml` (sama nazwa pliku), Environment `nuget` |
| GitHub → ustawienia repozytorium → **Secrets and variables → Actions** | Sekret `NUGET_USER`: nazwa profilu w nuget.org (nie adres e-mail) konta będącego właścicielem polityki |
| GitHub → ustawienia repozytorium → **Environments** | Środowisko `nuget`; dodaj wymaganych recenzentów, aby każde wydanie czekało na akceptację |

Polityka obejmuje wszystkie paczki należące do konta lub organizacji wybranej jako jej właściciel. Ten sam plik workflow obsługuje gałęzie utrzymaniowe `release/*`, więc jedna polityka wystarcza dla wszystkich linii wydań.

---

## Pytania

Otwórz [Issue na GitHub](https://github.com/tailored-apps/SharedComponents/issues) lub skontaktuj się z maintainerami projektu.
