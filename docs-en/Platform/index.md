# Target frameworks and runtime async

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Every `TailoredApps.Shared.*` package is built for several target frameworks at once (multi-targeting). NuGet picks the asset that matches your application's `TargetFramework` — there is nothing to configure.

---

## Supported target frameworks

| Libraries | Target frameworks |
|---|---|
| `DateTime`, `Email.Models`, `MediatR.Caching`, `Querying` | `netstandard2.0`, `netstandard2.1` |
| `Email`, `Email.Office365`, `EntityFramework`, `EntityFramework.UnitOfWork.WebApiCore`, `ExceptionHandling`, `MediatR`, `MediatR.Email`, `MediatR.ML`, `MediatR.PagedRequest`, `Payments` and every payment provider, `WebApi` | `net8.0`, `net9.0`, `net10.0`, `net11.0` |

The `netstandard` libraries run on all of the .NET versions above, .NET 11 included — they do not need a dedicated asset.

### Framework-aligned dependencies

Packages tied to the runtime generation (`Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore*`, `System.Configuration.ConfigurationManager`) are referenced separately for each target framework: the `net11.0` asset depends on the `11.x` line, `net10.0` on `10.x`, and so on. An application on .NET 11 therefore does not pull in packages from the previous generation.

!!! note "Exception: WebApi on net11.0"
    On the day .NET 11 was released, nuget.org had no `11.x` line of `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` yet, so the `net11.0` asset of `WebApi` temporarily uses their `10.x` line.

---

## Runtime async (.NET 11)

In .NET 11 the runtime itself can suspend and resume `async` methods. The C# compiler then no longer generates a separate state machine type for every `async` method — the method stays an ordinary method marked with the `MethodImplOptions.Async` flag. The goal is lower overhead for asynchronous calls and smaller IL. Source code and public API are unchanged: signatures still return `Task` / `ValueTask`.

The libraries in this repository use it in their `net11.0` assets:

| Asset in the package | How `async` methods are compiled |
|---|---|
| `net11.0` | **runtime async** |
| `net8.0`, `net9.0`, `net10.0` | compiler-generated state machine (unchanged) |
| `netstandard2.0`, `netstandard2.1` | compiler-generated state machine (unchanged) |

### What it means for your application

- **Nothing to switch on.** An application on `net11.0` gets the runtime-async asset automatically; applications on `net8.0`–`net10.0` get exactly what they got before.
- **Your application does not have to use runtime async itself.** Classically compiled code can freely call library methods compiled as runtime async and the other way round — this repository's tests are compiled classically and run on both `net10.0` and `net11.0`.
- **No environment variables** and no `<EnablePreviewFeatures>` are needed.

!!! warning "Requires CoreCLR or NativeAOT"
    Runtime async works on CoreCLR and NativeAOT. The Mono runtime — used by **Blazor WebAssembly**, among others — cannot execute such methods in .NET 11. If you want to use one of the libraries that contain `async` code on the Blazor WebAssembly client side on `net11.0`, build it with runtime async switched off (see below). The `netstandard` libraries (`DateTime`, `Email.Models`, `MediatR.Caching`, `Querying`) are not affected.

!!! info "Feature status"
    In .NET 11 runtime async is an opt-in feature that Microsoft's documentation still describes as a preview: [What's new in the .NET 11 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime).

### How it is enabled in the repository

One place: `src/Directory.Build.targets`. The file appends `runtime-async=on` to the compiler's `Features` property only for target frameworks compatible with `net11.0`:

```xml
<PropertyGroup Condition="'$(UseRuntimeAsync)' != 'false' and '$(TargetFramework)' != '' and $([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net11.0'))">
  <Features>$(Features);runtime-async=on</Features>
</PropertyGroup>
```

The condition is required: setting the flag for `net10.0` fails the build with `SYSLIB5007` / `CA2252`, and on older frameworks the flag is silently ignored.

### Switching it off

| Scope | How |
|---|---|
| Whole build | `dotnet build TailoredApps.Shared.sln -p:UseRuntimeAsync=false` |
| One library | `<UseRuntimeAsync>false</UseRuntimeAsync>` in its `.csproj` file |

With the switch off, the `net11.0` asset is compiled like the others — with state machines.

---

## Building the repository

- **.NET SDK 11.0.100 or newer** is required — it builds every target framework from `net8.0` to `net11.0`.
- The tests run on `net10.0` and `net11.0`, so both runtimes are needed locally.
- The CI workflows install the `10.0.x` SDK (runtime for the `net10.0` tests) and `11.0.x` (build).

```bash
dotnet build TailoredApps.Shared.sln
dotnet test TailoredApps.Shared.sln
```

### Adding a new library

- Use `<TargetFrameworks>net8.0;net9.0;net10.0;net11.0</TargetFrameworks>`.
- Reference runtime-aligned packages conditionally, once per target framework (`Condition="'$(TargetFramework)' == 'net11.0'"` → `Version="11.*"`).
- Do not set `Features` in the `.csproj` — runtime async is inherited from `src/Directory.Build.targets`.
- Add `net11.0` to the test project's `<TargetFrameworks>` so the `net11.0` asset is actually tested.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps Shared Components — target frameworks and runtime async — AI agent instructions

You are working with TailoredApps.Shared.* packages or with the SharedComponents repository.

### Registration
- None. Choosing a target framework needs no Program.cs calls — NuGet selects the asset that
  matches the application's TargetFramework.

### What gets configured
- Frameworks: net8.0, net9.0, net10.0, net11.0. Only these are netstandard2.0/netstandard2.1:
  DateTime, Email.Models, MediatR.Caching, Querying.
- The net11.0 assets have their async methods compiled as runtime async; every other asset uses
  compiler-generated state machines. The public API is identical on every framework.
- Microsoft.Extensions.* / EF Core dependencies are aligned with the framework (net11.0 -> 11.x).

### Configuration
- In the repository: the UseRuntimeAsync MSBuild property (on by default for net11.0+), handled
  in src/Directory.Build.targets. Switch off with -p:UseRuntimeAsync=false or
  <UseRuntimeAsync>false</UseRuntimeAsync> in the library's csproj.
- In an application consuming the packages: no settings.

### Rules
- Do not add <Features>runtime-async=on</Features> or <EnablePreviewFeatures> to library csproj
  files — the switch is central and conditioned on the target framework.
- Do not set runtime-async for net10.0 or older frameworks: the build fails with
  SYSLIB5007 / CA2252.
- Do not use the net11.0 assets of libraries containing async code in Blazor WebAssembly (Mono
  does not support runtime async in .NET 11) — build them with UseRuntimeAsync=false.
- New library: TargetFrameworks net8.0;net9.0;net10.0;net11.0, runtime-aligned dependencies
  conditioned per framework, test project on net10.0;net11.0.
- Build the repository with .NET SDK 11.0.100 or newer.
```
