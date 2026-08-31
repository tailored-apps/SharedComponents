# Aktualizacje zależności

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-SharedComponents-181717?logo=github)](https://github.com/tailored-apps/SharedComponents)

Historia automatycznych aktualizacji referencji NuGet w bibliotekach tego repozytorium. Strona jest generowana przez workflow **Bump dependencies** (`.github/workflows/bump-deps.yml`) i dopisywana do każdego PR-a z podbiciem wersji paczek.

!!! info "Strona generowana automatycznie"
    Treść pomiędzy znacznikami `BUMP-LOG` jest nadpisywana przez `scripts/New-BumpDocs.ps1`.
    Nie edytuj jej ręcznie — zmiany zostaną utracone przy kolejnym przebiegu.

---

## Jak działa automatyczny bump

Workflow uruchamia się w każdy poniedziałek o 06:00 UTC (oraz ręcznie przez *workflow dispatch*)
i wykonuje `scripts/Bump-Deps.ps1`, który dla każdego `*.csproj` sprawdza w nuget.org najnowszą
stabilną wersję każdej referencji `PackageReference`.

| Reguła | Zachowanie |
|---|---|
| Wersje wildcard (`10.*`, `2.*`) | pomijane — aktualizują się same przy `restore` |
| Wersje pre-release | ignorowane |
| Licencja nowej wersji spoza allowlisty (`MIT`, `Apache-2.0`) | **SKIP** |
| Zmiana licencji między wersją obecną a najnowszą | **SKIP** (wykrywanie relicencjonowania) |
| Brak zadeklarowanej licencji SPDX | **SKIP** (chyba że `-AllowUndeclaredLicense`) |
| Pakiet na liście `-Ignore` (np. `MediatR` >= 13.0.0, RPL-1.5) | clamp do najwyższej wersji poniżej progu |

Po podbiciu wersji workflow buduje całe rozwiązanie (`dotnet build`), generuje wpis na tej stronie
i dopiero wtedy tworzy PR z etykietami `dependencies` i `automated`.

### Uruchomienie lokalne

```bash
# podgląd bez modyfikacji plików
pwsh ./scripts/Bump-Deps.ps1 -DryRun -RecordsPath ./bump-records.json

# wygenerowanie wpisu dokumentacji z raportu
pwsh ./scripts/New-BumpDocs.ps1 -RecordsPath ./bump-records.json
```

---

## Dziennik zmian

<!-- BUMP-LOG:START -->

## 2026-08-31

Zaktualizowano **1** referencję pakietu w **1** projekcie.

Źródło: [przebieg workflow](https://github.com/tailored-apps/SharedComponents/actions/runs/33394567918).

**`src/TailoredApps.Shared.EntityFramework/TailoredApps.Shared.EntityFramework.csproj`**

| Pakiet | Z wersji | Na wersję | Licencja |
|---|---|---|---|
| `System.Linq.Dynamic.Core` | `1.7.3` | `1.7.4` | `Apache-2.0` |

<!-- BUMP-LOG:END -->
