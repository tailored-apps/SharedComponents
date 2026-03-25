# Contributing

Dziękujemy za zainteresowanie współtworzeniem **TailoredApps Shared Components**! Ten dokument opisuje proces contributingu oraz **obowiązkowe wymagania dokumentacyjne**.

---

## Zasady ogólne

1. **Fork → Feature Branch → PR** — nie commituj bezpośrednio na `master`
2. Nazwa brancha: `feature/<short-name>` lub `fix/<short-name>`
3. Jeden PR = jedna zmiana merytoryczna
4. Wszystkie testy muszą przechodzić przed otwarciem PR
5. Kod musi posiadać XML docs (summary) dla publicznych typów i metod

---

## Środowisko lokalne

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

---

## Pytania

Otwórz [Issue na GitHub](https://github.com/tailored-apps/SharedComponents/issues) lub skontaktuj się z maintainerami projektu.
