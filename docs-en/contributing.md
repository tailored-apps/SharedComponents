# Contributing

Thank you for your interest in contributing to **TailoredApps Shared Components**! This document describes the contribution process and **mandatory documentation requirements**.

---

## General Rules

1. **Fork → Feature Branch → PR** — do not commit directly to `master`
2. Branch naming: `feature/<short-name>` or `fix/<short-name>`
3. One PR = one substantive change
4. All tests must pass before opening a PR
5. Code must have XML docs (summary) for public types and methods

---

## Local Setup

```bash
git clone https://github.com/tailored-apps/SharedComponents.git
cd SharedComponents

# .NET
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
export PATH="$PATH:/opt/homebrew/opt/dotnet/bin"
dotnet restore

# Documentation
pip install mkdocs-material
mkdocs serve  # http://127.0.0.1:8000
```

---

## 🔴 IRON RULE OF DOCUMENTATION

> **Every new library in this repo MUST have a documentation page in `docs/Libraries/`.**
>
> **PR without documentation = PR rejected.**

### Requirements for each documentation page

Each `docs/Libraries/<LibraryName>/index.md` page must contain:

1. **Header + badges** — library name, NuGet and license badges
2. **Description** — in **Polish** 🇵🇱 and in **English** 🇬🇧
3. **Installation** — `dotnet add package ...`
4. **DI Registration** — example with `Program.cs`
5. **Usage example** — real, complete C# code (not a toy example)
6. **API Reference** — table/list of main interfaces and classes
7. **🤖 AI Agent Prompt** — ready-made prompt to paste into an AI agent context

### Navigation update

After adding a library page, update the `nav:` section in `mkdocs.yml` and the library table on `docs/index.md`.

### Library page template

```markdown
# TailoredApps.Shared.XXXXX

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.XXXXX)](https://www.nuget.org/packages/TailoredApps.Shared.XXXXX/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

## 🇵🇱 Opis

[Full description in Polish...]

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

| Type | Description |
|------|-------------|
| \`IXxx\` | ... |

## 🤖 AI Agent Prompt

\`\`\`markdown
## TailoredApps.Shared.XXXXX — AI Agent Instructions

You are using the TailoredApps.Shared.XXXXX library in a .NET project.

### Registration
...

### Usage
...

### Rules
- ...
\`\`\`
```

---

## Pre-PR Checklist

- [ ] Code compiles without errors (`dotnet build`)
- [ ] Tests pass (`dotnet test`)
- [ ] XML docs added to public types
- [ ] Documentation page in `docs/Libraries/`
- [ ] `mkdocs.yml` nav updated
- [ ] Table on `docs/index.md` updated
- [ ] `mkdocs build --strict` passes without errors

---

## Questions

Open an [Issue on GitHub](https://github.com/tailored-apps/SharedComponents/issues) or contact the project maintainers.
