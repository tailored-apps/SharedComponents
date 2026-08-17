# Żelazna Zasada Dokumentacji — TailoredApps SharedComponents

Każda nowa biblioteka w tym repo MUSI posiadać stronę dokumentacji w `docs/Libraries/`.

## Wymagania dla każdej strony dokumentacji:

1. Opis działania w języku **polskim** i **angielskim**
2. Instrukcja instalacji (`dotnet add package`)
3. Przykład rejestracji w DI (`Program.cs`)
4. Przykład użycia (realny kod C#)
5. Sekcja **🤖 AI Agent Prompt** — gotowy prompt do wklejenia w kontekst agenta AI
6. Aktualizacja `mkdocs.yml` nav
7. Aktualizacja tabeli na `docs/index.md`

## Weryfikacja:

PR bez dokumentacji = PR odrzucony.

---

## Szczegółowa struktura strony dokumentacji

Każda strona musi zawierać następujące sekcje (w tej kolejności):

### 1. Header + badges

```markdown
# TailoredApps.Shared.XXXXX
[![NuGet](badge)] [![License](badge)]
```

### 2. Opis działania (dwujęzyczny)

```markdown
## 🇵🇱 Opis
[Pełny opis po polsku — problem, rozwiązanie, kiedy używać]

## 🇬🇧 Description
[Full description in English]
```

### 3. Instalacja

```bash
dotnet add package TailoredApps.Shared.XXXXX
```

### 4. Rejestracja w DI

```csharp
// Program.cs
builder.Services.AddXxx();
```

### 5. Przykład użycia

Realny, kompletny przykład kodu C# — nie toy example.

### 6. API Reference

Tabela głównych typów publicznych z opisem.

### 7. 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.XXXXX — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.XXXXX w projekcie .NET.

### Rejestracja
[jak zarejestrować — kod]

### Użycie
[jak używać — konkretne wzorce kodu]

### Zasady
- [zasada 1]
- [zasada 2]
```

---

## Automatyczne bumpy zależności

PR-y tworzone przez workflow **Bump dependencies** też muszą spełniać tę zasadę — zmieniają
`*.csproj` pod `src/`, więc *Docs guard* wymaga od nich zmiany w `docs/` i `docs-en/`.
Robi to za nie `scripts/New-BumpDocs.ps1`, uruchamiany zaraz po `scripts/Bump-Deps.ps1`:
dopisuje datowany wpis (projekt → pakiet → wersja z/na → licencja) na stronach
`docs/Dependencies/index.md` i `docs-en/Dependencies/index.md`.

Zasady:

- Nie edytuj ręcznie treści między znacznikami `<!-- BUMP-LOG:START -->` a `<!-- BUMP-LOG:END -->`
  — jest nadpisywana przy każdym przebiegu. Tekst poza znacznikami jest zachowywany.
- Etykieta `skip-docs` na takim PR-ze nie jest potrzebna; jeżeli jej używasz, robisz to świadomie
  (np. przy ręcznej poprawce, która nie przeszła przez skrypt).
- Bumpy nie są bibliotekami — nie wymagają sekcji 🤖 AI Agent Prompt ani wpisu w tabeli `docs/index.md`.

## Checklist PR

- [ ] Plik `docs/Libraries/<Nazwa>/index.md` (lub odpowiednia strona) istnieje
- [ ] Zawiera opis PL i EN
- [ ] Zawiera przykład DI registration
- [ ] Zawiera realny przykład użycia
- [ ] Zawiera sekcję 🤖 AI Agent Prompt
- [ ] `mkdocs.yml` nav zaktualizowany
- [ ] `docs/index.md` tabela zaktualizowana
- [ ] `mkdocs build --strict` przechodzi bez błędów
