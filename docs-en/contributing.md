# Contributing

Thank you for your interest in contributing to **TailoredApps Shared Components**! This document describes the contribution process and **mandatory documentation requirements**.

---

## General Rules

1. **Fork → Feature Branch → PR** — do not commit directly to `master`
2. Branch naming: `feature/<short-name>` or `fix/<short-name>`
3. One PR = one substantive change
4. All tests must pass before opening a PR
5. Code must have XML docs (summary) for public types and methods
6. One namespace per file, declared without a block (`namespace TailoredApps.Shared.Xxx;`). `.editorconfig` enforces the style; fix existing code with `dotnet format style TailoredApps.Shared.slnx --diagnostics IDE0161 --severity info`

---

## Local Setup

**.NET SDK 11.0.100 or newer** is required — it builds both target frameworks (`net10.0` and `net11.0`). The tests run on `net10.0` and `net11.0`, so both runtimes are needed. The conventions for target frameworks and runtime async are described on the [Target frameworks and runtime async](Platform/index.md) page.

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

### Solution and build output

The repository uses the XML solution format — `TailoredApps.Shared.slnx` (opened by the `dotnet` CLI and by Visual Studio 2022 17.13 or newer).

```bash
dotnet build TailoredApps.Shared.slnx
dotnet test TailoredApps.Shared.slnx
```

The root `Directory.Build.props` enables `UseArtifactsOutput`, so projects **no longer** create their own `bin/` and `obj/` folders. Everything goes to a single git-ignored `artifacts/` directory:

| Path | Contents |
|------|----------|
| `artifacts/bin/<Project>/<configuration>_<tfm>/` | compiled libraries, e.g. `artifacts/bin/TailoredApps.Shared.WebApi/release_net10.0/` |
| `artifacts/obj/<Project>/` | intermediate files |
| `artifacts/package/<configuration>/` | NuGet packages (`*.nupkg`) for every library — the release workflow publishes from here |

To clean the build, delete the `artifacts/` directory. Add a new library to the solution with `dotnet sln TailoredApps.Shared.slnx add <path to .csproj>` — the settings from `Directory.Build.props` apply automatically: target frameworks, language version, nullable, package metadata (author, MIT licence, project URL) and NuGet package generation. Package versions live in `Directory.Packages.props`, and `nuget.config` limits the sources to nuget.org. `*.user` files and the `.vs/`, `.idea/`, `.vscode/` folders are never committed (they are in `.gitignore`).

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

### Both languages and the package README

- The site has two trees: `docs/` (Polish) and `docs-en/` (English). Every page exists in both, with the same relative path, and is registered
  in both `mkdocs.yml` and `mkdocs-en.yml`; the library table is on both `docs/index.md` and `docs-en/index.md`. A change to a library updates both.
- Every library has a `README.md` next to its csproj: what the package is, installation, registration, a usage example, configuration, and a link
  to its page on this site. It is packed into the NuGet package automatically (`src/Directory.Build.props`) and is what nuget.org shows - nothing
  has to be added to the csproj.
- Code samples must compile against the current source: check type, method and configuration key names in the code, not from memory.

### The demo application and the integration tests

The [demo application](Demo/index.md) (`samples/TailoredApps.Shared.Demo`) uses every library, and `tests/TailoredApps.Shared.IntegrationTests`
drives it over HTTP. A new library is added to the demo too, with a test of its scenario; a change after which a library is used differently
updates the demo. The demo runs without a network: an external service is replaced at the transport level (an HTTP handler), never by
changing the library.

### Library page template

```markdown
# TailoredApps.Shared.XXXXX

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.XXXXX)](https://www.nuget.org/packages/TailoredApps.Shared.XXXXX/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

## 🇵🇱 Opis

[Full description in Polish...]

## 🇬🇧 Description

[Full description in English...]

## Installation

\`\`\`bash
dotnet add package TailoredApps.Shared.XXXXX
\`\`\`

## DI registration

\`\`\`csharp
// Program.cs
builder.Services.AddXxx();
\`\`\`

## Usage example

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
- [ ] `dotnet build TailoredApps.Shared.slnx -c Release -p:ValidatePublicApi=true` passes (no change incompatible with 2.0.0)

---

## Public API compatibility

The `master` branch is the **3.0 line in a test phase** (`3.0.0-preview.N`): breaking changes are allowed until `3.0.0`, but only on
purpose. The `release/2.1` branch is the 2.1.x fix line, compatible with `2.0.0`. On `master` the `TailoredApps.Shared.MediatR*` libraries are
left out of the comparison (they moved from MediatR to ForkForward.Mediator); the others are still compared, so that a breaking change does
not happen by accident.

The 2.x line must stay compatible with `2.0.0`: code compiled against any 2.x version has to work with every later 2.x. The
*Build & Test PR* check compares the public API of every library with its `2.0.0` package on nuget.org and fails on a breaking change
(removing a public type or member, or changing its signature - `CP0001`, `CP0002`, …). Adding API is allowed.

```bash
# the same locally
dotnet build TailoredApps.Shared.slnx -c Release -p:ValidatePublicApi=true
```

- The baseline version is set by `PublicApiBaselineVersion` in `src/Directory.Build.targets`.
- A new library: until its first release add `<SkipPublicApiValidation>true</SkipPublicApiValidation>` to its csproj,
  and after the release replace it with `<PublicApiBaselineVersion>` holding the first version it was published in.
- On the 2.x line, instead of removing a member, mark it `[Obsolete]` and add a new one; the removal waits for a major version.
- A deliberate removal on the 3.0 line is recorded in the library's `CompatibilitySuppressions.xml` - member by member, so the rest of its API
  stays guarded. `dotnet build src/<Library>/<Library>.csproj -c Release -p:ValidatePublicApi=true -p:ApiCompatGenerateSuppressionFile=true`
  generates the file; review it before committing - it must hold only what you remove on purpose.

---

## Releasing (maintainers)

Pushing a tag `vX.Y.Z` (or a pre-release such as `v2.0.0-preview.1`) runs the *Create Release* workflow (`.github/workflows/release.yml`): it builds every library with the version taken from the tag, runs the tests, and publishes the packages from `artifacts/package/release` to nuget.org. The job fails if any package could not be published. *Release notes* creates the GitHub Release from the merged pull requests.

Packages are published through **nuget.org Trusted Publishing** — the repository stores no NuGet API key. The job requests a GitHub OIDC token and nuget.org exchanges it for an API key that is valid for one hour. This needs a one-time setup:

| Where | Setting |
|-------|---------|
| nuget.org → account menu → **Trusted Publishing** → new policy | Repository Owner `tailored-apps`, Repository `SharedComponents`, Workflow File `release.yml` (file name only), Environment `nuget` |
| GitHub → repository settings → **Secrets and variables → Actions** | Secret `NUGET_USER`: the nuget.org profile name (not the e-mail address) of the account that owns the policy |
| GitHub → repository settings → **Environments** | Environment `nuget`; add required reviewers to make every release wait for approval |

The policy covers every package owned by the account or organisation chosen as its owner. The same workflow file serves the `release/*` maintenance branches, so one policy is enough for all release lines.

---

## Questions

Open an [Issue on GitHub](https://github.com/tailored-apps/SharedComponents/issues) or contact the project maintainers.
