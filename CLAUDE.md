# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Build & Test Commands

Requires the .NET SDK 11.0.100 or newer. Libraries and tests target `net10.0;net11.0`, so the .NET 10
runtime has to be installed as well for the `net10.0` test runs.

## Build
- Build solution: `dotnet build TailoredApps.Shared.slnx`
- Build specific project: `dotnet build src/TailoredApps.Shared.DateTime/TailoredApps.Shared.DateTime.csproj`
- Every library has `GeneratePackageOnBuild`, so a build also packs: `-c Release` leaves all 24 packages in `artifacts/package/release/`.

## Build output
- `Directory.Build.props` sets `UseArtifactsOutput`, so nothing is written to per-project `bin/` or `obj/` folders.
- Everything lands in `artifacts/` (git-ignored): `artifacts/bin/<Project>/<configuration>_<tfm>/`, `artifacts/obj/<Project>/`, and NuGet packages in `artifacts/package/<configuration>/`.

## Test
- Run all tests: `dotnet test TailoredApps.Shared.slnx`
- Run specific test project: `dotnet test tests/TailoredApps.Shared.DateTime.Tests/TailoredApps.Shared.DateTime.Tests.csproj`
- Run single test: `dotnet test --filter "FullyQualifiedName=TailoredApps.Shared.DateTime.Tests.DateTimeProviderTests.As_Library_User_When_Get_Now_I_Get_Mine_System_Date_In_Current_Timezone_And_Local_Kind"`
- Run on one framework only: add `-f net10.0` (or `-f net11.0`); without it every test runs twice.
- With coverage, as CI does: `dotnet test TailoredApps.Shared.slnx -p:CollectCoverage=true -p:CoverletOutputFormat=opencover`.
  Write MSBuild properties as `-p:Name=value`: Git Bash on Windows rewrites `/p:` into a path and the command fails with `MSB1008`.

## Docs
- `python -m mkdocs build --strict -f mkdocs.yml` and `python -m mkdocs build --strict -f mkdocs-en.yml` - both must pass (CI pins `mkdocs-material==9.7.7`).

## Public API compatibility
- `dotnet build src/<Library>/<Library>.csproj -c Release -p:EnablePackageValidation=true -p:PackageValidationBaselineVersion=<published version>`
  compares the library's public API with that version on nuget.org and fails on breaking changes. Run it per library, not on the solution
  (two test projects are not marked `IsPackable=false`). Required for anything going to `release/1.x`, which must stay compatible with `1.6.0`.

# Architecture

The repository is 24 NuGet packages (`TailoredApps.Shared.*`) under `src/`, with one test project per library family under `tests/`.
There is no application project: every library is consumed through dependency-injection extension methods. All packages are released
together under a single version taken from the git tag.

## Build configuration shared by every project
- `Directory.Build.props` (root): artifacts output and `LangVersion=latest`, so both target frameworks compile with the same C# version.
- `src/Directory.Build.targets`: turns on runtime async for the `net11.0` assets (`UseRuntimeAsync`, opt out per library or per build). Never set `Features` or `EnablePreviewFeatures` in a csproj.
- Packages tied to the runtime generation (`Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore*`) are referenced once per target framework
  with a `Condition` (`net10.0` -> `10.*`, `net11.0` -> `11.*`). Adding such a dependency means adding both lines.
- Most versions float (`10.*`, `1.*`). The weekly *Bump dependencies* workflow (`scripts/Bump-Deps.ps1`) raises the pinned ones and writes its own docs entry.
- Package metadata is repeated in each csproj; there is no central package management yet.

## Library families and how they fit together
- **Payments** - `TailoredApps.Shared.Payments` holds the contracts: `IPaymentProvider` (channels, `RequestPayment`, `GetStatus`, and the
  legacy `TransactionStatusChange`), `IWebhookPaymentProvider` (`HandleWebhookAsync` returning `PaymentWebhookResult` Ok / Ignore / Fail) and the
  `IPaymentService` facade that picks a provider by its `Key`. Hosts call `AddPayments(...)` and `RegisterPaymentProvider<T>()`.
  Each provider is its own package (`Payments.Provider.*`) with the same shape: `XServiceOptions` bound from `Payments:Providers:X`, a service
  caller (HTTP calls and signature verification), `XProvider`, and a `RegisterXProvider()` extension. HotPay is the smallest one to copy.
  Signature checks must fail closed and use `Payments/Security/WebhookSignature` (constant time); identifiers placed in gateway URLs go through `PaymentIdentifier`.
- **MediatR** - `PipelineRegistration` adds the behaviours in a fixed order: Logging -> Validation (FluentValidation) -> Caching -> Fallback -> Retry.
  A request opts in by having an `ICachePolicy<,>`, `IFallbackHandler<,>` or `IRetryableRequest<,>` implementation, which assembly scanning (Scrutor) registers.
  `ICache` wraps `IDistributedCache` and reports a miss as `default(T)`. Satellites: `MediatR.Caching` (marker contracts), `MediatR.Email`,
  `MediatR.PagedRequest`, and `MediatR.ML`, whose namespace is `TailoredApps.Shared.MediatR.ImageClassification`, not the project name.
- **EntityFramework** - a unit of work over `DbContext` with hooks (before/after save, commit, rollback) that also drive audit change collection,
  plus paging/sorting/filtering extensions (dynamic LINQ with sort-field validation). `EntityFramework.UnitOfWork.WebApiCore` adds the MVC
  transaction filter (`AddUnitOfWorkForWebApi<,>`). `Querying` holds the framework-free query contracts used by both EntityFramework and `MediatR.PagedRequest`.
- **ExceptionHandling** - `AddExceptionHandlingForWebApi<,>` plus a filter and middleware; an `IExceptionHandlingProvider` maps exceptions to the
  response model. Unexpected exceptions are answered with 500 and a generic message.
- **Email** - `IEmailProvider` with SMTP, console and Office365 providers (`Register*Provider()`), and message builders that HTML-encode template variables.
- **WebApi** - `builder.AddTailoredWebApiDefaults()` and `app.MapTailoredWebApiDefaults()` wire OpenTelemetry (OTLP), health checks and HTTP
  client resilience with service discovery from the `WebApiDefaults` configuration section.

`Payments.Tests` covers the core library and every provider; `MediatR.Tests` also covers `MediatR.Email` and `MediatR.PagedRequest`; `Email.Tests` also covers `Email.Office365`.

# Branches and releases

- `master` is the **2.x** line: `net10.0` and `net11.0` only, breaking changes allowed until `2.0.0`. Pre-releases are tagged `v2.0.0-preview.N`.
- `release/1.x` is the **1.6.x** maintenance line (it still builds `net8.0`, `net9.0` and the `netstandard` assets): security fixes only,
  tagged `v1.6.N` from that branch, binary compatible with `1.6.0`.
- Pushing any `v*` tag publishes all 24 packages to nuget.org and creates the GitHub Release. It cannot be undone, so never push a tag
  unless explicitly asked. `release.yml` uses nuget.org Trusted Publishing (GitHub OIDC, environment `nuget`, secret `NUGET_USER`) - there is
  no stored API key. The job fails if any package push fails; when checking a release, read the push step, not just the run colour.
- Pull requests target `master` (or `release/**`) and are squash-merged. PR checks: *Build & Test PR* (Windows) and *Docs guard*.
- Consumers mostly build with the .NET 10 SDK (C# 14). Keep C# 15 `union` types out of the public API - an older compiler can only reach
  them through `.Value`. `closed` hierarchies and other C# 14/15 syntax inside the libraries are fine.

# Repository conventions

- `.gitattributes` declares `* binary`: `git diff` and `--stat` show every file as `Bin`. Use `git diff --text`; files changed on both sides of a merge conflict unless a merge driver is forced.
- Line endings and BOMs are mixed (many sources are CRLF with a BOM). Preserve what a file already uses when editing it.
- Workflows pin every action to a commit SHA with a version comment (Dependabot keeps them current) and declare least-privilege `permissions`. Keep both when editing workflows.
- `docs/Security/index.md` (and `docs-en/`) records the security review, the behaviour it changed and the follow-ups that are still open.

# Code Style Guidelines

## Naming Conventions
- Use PascalCase for class, method, property, interface names
- Interface names should start with "I" (e.g., `IDateTimeProvider`)
- Use descriptive names for types and members
- Use verb phrases for method names (e.g., `SendMail`)

## Code Organization
- Use namespaces that match directory structure
- One file-scoped namespace per file (`namespace X;`), as `.editorconfig` requires
- Group related functionality in dedicated projects
- XML documentation on public interfaces and classes

## Error Handling
- Use exceptions for exceptional cases, not control flow
- Validate method arguments with explicit checks

## Testing
- Use xUnit for unit tests
- Test method naming format: `When_Condition_Should_ExpectedBehavior`
- Arrange-Act-Assert pattern for test structure

# Documentation (mandatory for every library)

Documentation is part of "done". Any PR that adds or changes a shared library MUST include its
documentation in the **same PR** — a PR without it is incomplete and will be rejected.
`DOCUMENTATION_RULE.md` is the authoritative spec; always follow it. The essentials:

## Docs spec — always include
- A library page in **both** languages: `docs/Libraries/<Name>/index.md` (Polish) and
  `docs-en/Libraries/<Name>/index.md` (English).
- Each page, in this order: header + NuGet/License badges → description → installation
  (`dotnet add package ...`) → DI registration (`Program.cs`) → real usage example → API Reference
  table → **🤖 AI Agent Prompt** section.
- Register the page in the `nav` of **both** `mkdocs.yml` and `mkdocs-en.yml`.
- Add a row to the library table in **both** `docs/index.md` and `docs-en/index.md`.
- `mkdocs build --strict` must pass for both configs (no orphan pages, no broken links). Never leave a
  stray page outside the nav.

## AI-setup spec — always include
Every library page MUST carry a `## 🤖 AI Agent Prompt` section: a ready-to-paste prompt block that
tells an AI coding agent how to wire the library into a project. It must state:
- **Registration** — the exact `Program.cs` calls.
- **What gets configured** — the public surface.
- **Configuration** — the keys or options.
- **Rules** — the dos and don'ts the agent must follow.

## Package- and IDE-facing docs
- A `README.md` in the library project, packed into the NuGet package via `<PackageReadmeFile>`.
- XML doc comments on every public type and member (`<GenerateDocumentationFile>True</GenerateDocumentationFile>`).

When in doubt, mirror an existing page such as `docs/Libraries/EntityFramework/UnitOfWork.WebApiCore.md`.

## Automated dependency bumps
`.github/workflows/bump-deps.yml` changes `src/**/*.csproj`, so it must satisfy the same rule.
`scripts/Bump-Deps.ps1 -RecordsPath <json>` emits a machine-readable BUMP/SKIP report and
`scripts/New-BumpDocs.ps1 -RecordsPath <json>` turns it into a dated entry on
`docs/Dependencies/index.md` + `docs-en/Dependencies/index.md`. Only the region between the
`<!-- BUMP-LOG:START -->` / `<!-- BUMP-LOG:END -->` markers is rewritten — never hand-edit it.

## Enforcement
- **CI (blocking):** the *Docs guard* workflow (`.github/workflows/docs-guard.yml`) fails a PR when
  `src/**` changed without matching `docs/**` + `docs-en/**` updates (escape hatch: add the `skip-docs`
  label), and runs `mkdocs build --strict` for both sites. Add both checks to branch protection so they
  block merge.
- **Local (pre-push):** enable the shared hook once per clone — `git config core.hooksPath .githooks`.
  It blocks a push that changes `src/**` without docs. Bypass a single push with `SKIP_DOCS_CHECK=1 git push`.
- **Changelog:** GitHub Releases are generated from merged PRs on every `v*` tag; note categories live in
  `.github/release.yml`. Label PRs (`new-library`, `feature`, `bug`, `documentation`, `dependencies`) so
  they group correctly.