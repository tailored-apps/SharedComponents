<#
.SYNOPSIS
    Bump NuGet PackageReference versions to latest stable, with license-awareness.

.DESCRIPTION
    Scans all *.csproj files, queries nuget.org for latest stable versions, and
    rewrites <PackageReference Version="..."> values when safe.

    Rules:
      - Wildcard versions (e.g. "10.*", "2.*") are left alone — they self-update at restore.
      - The latest version's SPDX license expression must be in -AllowedLicenses.
      - If the SPDX license of the currently-pinned version differs from the latest's,
        the bump is SKIPPED (license-change rule — catches relicensing like MediatR).
      - Packages with no declared license expression are skipped by default
        (override with -AllowUndeclaredLicense).
      - Pre-release versions are ignored.

.PARAMETER AllowedLicenses
    SPDX expressions considered acceptable. Defaults to MIT and Apache-2.0.

.PARAMETER AllowUndeclaredLicense
    If set, packages whose latest version has no licenseExpression are bumped anyway.

.PARAMETER DryRun
    Print the bump plan but do not modify files.

.PARAMETER Path
    Root path to scan. Defaults to current directory.

.PARAMETER GithubOutput
    If set, also writes a summary line to $env:GITHUB_OUTPUT for CI consumption
    (keys: bumped=true|false, summary-file=<path>, records-file=<path>).

.PARAMETER RecordsPath
    Where to write the machine-readable JSON report of every BUMP and SKIP
    decision. Consumed by scripts/New-BumpDocs.ps1 to generate the dependency
    documentation. Defaults to a temp file when -GithubOutput is used.

.PARAMETER Ignore
    Hashtable of package name -> max allowed version (exclusive ceiling) or $null
    to block all bumps. Used to enforce known license-change cutoffs that the
    SPDX check cannot detect (e.g. MediatR 13+ switched to RPL-1.5 without
    declaring SPDX). Names are matched case-insensitively.

.EXAMPLE
    .\scripts\Bump-Deps.ps1 -DryRun
.EXAMPLE
    .\scripts\Bump-Deps.ps1 -AllowedLicenses MIT,Apache-2.0,BSD-3-Clause
#>
[CmdletBinding()]
param(
    [string[]]$AllowedLicenses = @('MIT', 'Apache-2.0'),
    [switch]$AllowUndeclaredLicense,
    [switch]$DryRun,
    [string]$Path = '.',
    [switch]$GithubOutput,
    [string]$RecordsPath,
    [hashtable]$Ignore = @{
        # MediatR 13+ relicensed Apache-2.0 -> RPL-1.5 (Lucky Penny Software).
        # SPDX is not published, so the license-change rule cannot catch it.
        'MediatR' = '13.0.0'
    }
)

# Normalize ignore keys to lowercase for case-insensitive lookup.
$normalizedIgnore = @{}
foreach ($k in $Ignore.Keys) { $normalizedIgnore[$k.ToLowerInvariant()] = $Ignore[$k] }
$Ignore = $normalizedIgnore

$ErrorActionPreference = 'Stop'

$script:cache = @{}
function Get-PackageInfo {
    param([string]$Name)
    if ($script:cache.ContainsKey($Name)) { return $script:cache[$Name] }
    $lower = $Name.ToLowerInvariant()
    $url = "https://api.nuget.org/v3/registration5-gz-semver2/$lower/index.json"
    try {
        $info = Invoke-RestMethod -Uri $url -ErrorAction Stop
    } catch {
        $info = $null
    }
    $script:cache[$Name] = $info
    return $info
}

function Get-CatalogEntries {
    param($Info)
    $entries = @()
    foreach ($page in $Info.items) {
        # Some pages are inlined; others must be fetched.
        if ($page.items) {
            $entries += $page.items
        } else {
            try {
                $inner = Invoke-RestMethod -Uri $page.'@id' -ErrorAction Stop
                if ($inner.items) { $entries += $inner.items }
            } catch { }
        }
    }
    return $entries
}

function Get-LatestStableEntry {
    param($Entries)
    $stable = $Entries |
        Where-Object { $_.catalogEntry.listed -ne $false -and $_.catalogEntry.version -notmatch '-' }
    if (-not $stable) { return $null }
    return ($stable |
        Sort-Object {
            $v = $_.catalogEntry.version -replace '\+.*$',''
            try { [version]$v } catch { [version]'0.0.0.0' }
        } |
        Select-Object -Last 1).catalogEntry
}

function Get-LicenseForVersion {
    param($Entries, [string]$Version)
    foreach ($e in $Entries) {
        if ($e.catalogEntry.version -eq $Version) { return $e.catalogEntry.licenseExpression }
    }
    return $null
}

function New-BumpRecord {
    param(
        [ValidateSet('bump', 'skip')][string]$Action,
        [string]$Project,
        [string]$ProjectPath,
        [string]$Package,
        [string]$From,
        [string]$To,
        [string]$License,
        [string]$Reason
    )
    [pscustomobject]@{
        action      = $Action
        project     = $Project
        projectPath = $ProjectPath
        package     = $Package
        from        = $From
        to          = $To
        license     = $License
        reason      = $Reason
    }
}

$root = Resolve-Path $Path
$rootPrefix = $root.Path.TrimEnd('\', '/')
$csprojs = @(Get-ChildItem -Path $root -Recurse -Filter *.csproj | Where-Object {
    # Skip anything inside a dot-directory — nested checkouts (.claude/worktrees),
    # tool caches and the like are not part of this repository's sources.
    $rel = $_.FullName.Substring($rootPrefix.Length).TrimStart('\', '/')
    -not ($rel -split '[\\/]' | Where-Object { $_.StartsWith('.') })
})
Write-Host "Scanning $($csprojs.Count) csproj files under $root" -ForegroundColor Cyan

$bumpedAny = $false
$summary = @()
$records = [System.Collections.Generic.List[object]]::new()

foreach ($file in $csprojs) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($file.FullName)

    $projName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $projPath = $file.FullName.Substring($rootPrefix.Length).TrimStart('\', '/') -replace '\\', '/'

    $changed = $false
    $refs = $xml.SelectNodes('//PackageReference')
    foreach ($ref in $refs) {
        $name = $ref.GetAttribute('Include')
        if (-not $name) { continue }
        $cur = $ref.GetAttribute('Version')
        if (-not $cur) { continue }
        if ($cur -match '\*') { continue }  # leave wildcards alone

        $lname = $name.ToLowerInvariant()
        if ($Ignore.ContainsKey($lname)) {
            $ceiling = $Ignore[$lname]
            if ($null -eq $ceiling) {
                Write-Host "SKIP $name (ignore list, all bumps blocked)" -ForegroundColor Yellow
                $summary += "SKIP $name (ignore list)"
                $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                            -Package $name -From $cur -Reason 'ignore list'))
                continue
            }
        }

        $info = Get-PackageInfo -Name $name
        if (-not $info) {
            Write-Warning "no nuget metadata for $name (skipping)"
            $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                        -Package $name -From $cur -Reason 'no nuget metadata'))
            continue
        }
        $entries = Get-CatalogEntries -Info $info
        if (-not $entries) {
            Write-Warning "no catalog entries for $name (skipping)"
            $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                        -Package $name -From $cur -Reason 'no catalog entries'))
            continue
        }
        $latest = Get-LatestStableEntry -Entries $entries
        if (-not $latest) { continue }

        # Apply ignore-list version ceiling, if any.
        if ($Ignore.ContainsKey($lname) -and $null -ne $Ignore[$lname]) {
            $ceiling = [version]($Ignore[$lname])
            try { $latestVer = [version]($latest.version -replace '\+.*$','') } catch { $latestVer = $null }
            if ($latestVer -and $latestVer -ge $ceiling) {
                # Latest is at or above ceiling — find highest stable strictly below it.
                $belowCeiling = $entries |
                    Where-Object {
                        $_.catalogEntry.listed -ne $false -and
                        $_.catalogEntry.version -notmatch '-'
                    } |
                    Where-Object {
                        try { ([version]($_.catalogEntry.version -replace '\+.*$','')) -lt $ceiling } catch { $false }
                    } |
                    Sort-Object {
                        try { [version]($_.catalogEntry.version -replace '\+.*$','') } catch { [version]'0.0.0.0' }
                    } |
                    Select-Object -Last 1
                if (-not $belowCeiling) {
                    Write-Host "SKIP $name ${cur} -> $($latest.version): ignore-list ceiling $ceiling, no version below it" -ForegroundColor Yellow
                    $summary += "SKIP $name (no version below ignore ceiling $ceiling)"
                    $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                                -Package $name -From $cur -To $latest.version `
                                -Reason "no version below ignore ceiling $ceiling"))
                    continue
                }
                Write-Host "CLAMP $name latest=$($latest.version) -> $($belowCeiling.catalogEntry.version) (ignore ceiling $ceiling)" -ForegroundColor DarkYellow
                $latest = $belowCeiling.catalogEntry
            }
        }

        if ($latest.version -eq $cur) { continue }

        $curLic    = Get-LicenseForVersion -Entries $entries -Version $cur
        $latestLic = $latest.licenseExpression

        if (-not $latestLic) {
            if (-not $AllowUndeclaredLicense) {
                Write-Host "SKIP $name ${cur} -> $($latest.version): no declared license" -ForegroundColor Yellow
                $summary += "SKIP $name -> $($latest.version) (no declared license)"
                $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                            -Package $name -From $cur -To $latest.version -Reason 'no declared license'))
                continue
            }
        } elseif ($curLic -and $curLic -ne $latestLic) {
            Write-Host "SKIP $name ${cur} -> $($latest.version): license changed ($curLic -> $latestLic)" -ForegroundColor Yellow
            $summary += "SKIP $name -> $($latest.version) (license $curLic -> $latestLic)"
            $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                        -Package $name -From $cur -To $latest.version -License $latestLic `
                        -Reason "license changed ($curLic -> $latestLic)"))
            continue
        } elseif ($latestLic -notin $AllowedLicenses) {
            Write-Host "SKIP $name ${cur} -> $($latest.version): license $latestLic not in allowlist" -ForegroundColor Yellow
            $summary += "SKIP $name -> $($latest.version) (license $latestLic not allowed)"
            $records.Add((New-BumpRecord -Action skip -Project $projName -ProjectPath $projPath `
                        -Package $name -From $cur -To $latest.version -License $latestLic `
                        -Reason "license $latestLic not in allowlist"))
            continue
        }

        Write-Host "BUMP $name ${cur} -> $($latest.version) ($latestLic) in $($file.Name)" -ForegroundColor Green
        $summary += "BUMP $name $cur -> $($latest.version) ($latestLic)"
        $records.Add((New-BumpRecord -Action bump -Project $projName -ProjectPath $projPath `
                    -Package $name -From $cur -To $latest.version -License $latestLic))
        $ref.SetAttribute('Version', $latest.version)
        $changed = $true
        $bumpedAny = $true
    }

    if ($changed -and -not $DryRun) {
        # Preserve file encoding (utf-8 no BOM is the .NET default for csproj).
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.OmitXmlDeclaration = -not $xml.FirstChild.NodeType.Equals([System.Xml.XmlNodeType]::XmlDeclaration)
        $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
        $settings.Indent = $false
        $writer = [System.Xml.XmlWriter]::Create($file.FullName, $settings)
        try { $xml.Save($writer) } finally { $writer.Dispose() }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
if ($summary.Count -eq 0) {
    Write-Host "No changes." -ForegroundColor Gray
} else {
    $summary | ForEach-Object { Write-Host $_ }
}

# Machine-readable report — feeds scripts/New-BumpDocs.ps1 so every bump PR also
# ships the documentation change the Docs guard requires.
if (-not $RecordsPath -and $GithubOutput) {
    $RecordsPath = Join-Path ([System.IO.Path]::GetTempPath()) 'bump-records.json'
}
if ($RecordsPath) {
    $payload = [ordered]@{
        generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
        records      = @($records)
    }
    $json = ConvertTo-Json -InputObject $payload -Depth 5
    [System.IO.File]::WriteAllText($RecordsPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Records written to $RecordsPath" -ForegroundColor Gray
}

if ($GithubOutput -and $env:GITHUB_OUTPUT) {
    "bumped=$($bumpedAny.ToString().ToLower())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    $sumPath = Join-Path ([System.IO.Path]::GetTempPath()) "bump-summary.txt"
    $summary | Out-File -FilePath $sumPath -Encoding utf8
    "summary-file=$sumPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "records-file=$RecordsPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if ($DryRun) { Write-Host "(dry run — no files modified)" -ForegroundColor Gray }
