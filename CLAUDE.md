# Build & Test Commands

## Build
- Build solution: `dotnet build TailoredApps.Shared.sln`
- Build specific project: `dotnet build src/TailoredApps.Shared.DateTime/TailoredApps.Shared.DateTime.csproj`

## Test
- Run all tests: `dotnet test TailoredApps.Shared.sln`
- Run specific test project: `dotnet test tests/TailoredApps.Shared.DateTime.Tests/TailoredApps.Shared.DateTime.Tests.csproj`
- Run single test: `dotnet test --filter "FullyQualifiedName=TailoredApps.Shared.DateTime.Tests.DateTimeProviderTests.As_Library_User_When_Get_Now_I_Get_Mine_System_Date_In_Current_Timezone_And_Local_Kind"`

# Code Style Guidelines

## Naming Conventions
- Use PascalCase for class, method, property, interface names
- Interface names should start with "I" (e.g., `IDateTimeProvider`)
- Use descriptive names for types and members
- Use verb phrases for method names (e.g., `SendMail`)

## Code Organization
- Use namespaces that match directory structure
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