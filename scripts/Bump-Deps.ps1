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
    (key: bumped, value: true|false).

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
    [switch]$GithubOutput
)

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

$root = Resolve-Path $Path
$csprojs = Get-ChildItem -Path $root -Recurse -Filter *.csproj
Write-Host "Scanning $($csprojs.Count) csproj files under $root" -ForegroundColor Cyan

$bumpedAny = $false
$summary = @()

foreach ($file in $csprojs) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($file.FullName)

    $changed = $false
    $refs = $xml.SelectNodes('//PackageReference')
    foreach ($ref in $refs) {
        $name = $ref.GetAttribute('Include')
        if (-not $name) { continue }
        $cur = $ref.GetAttribute('Version')
        if (-not $cur) { continue }
        if ($cur -match '\*') { continue }  # leave wildcards alone

        $info = Get-PackageInfo -Name $name
        if (-not $info) {
            Write-Warning "no nuget metadata for $name (skipping)"
            continue
        }
        $entries = Get-CatalogEntries -Info $info
        if (-not $entries) {
            Write-Warning "no catalog entries for $name (skipping)"
            continue
        }
        $latest = Get-LatestStableEntry -Entries $entries
        if (-not $latest) { continue }
        if ($latest.version -eq $cur) { continue }

        $curLic    = Get-LicenseForVersion -Entries $entries -Version $cur
        $latestLic = $latest.licenseExpression

        if (-not $latestLic) {
            if (-not $AllowUndeclaredLicense) {
                Write-Host "SKIP $name ${cur} -> $($latest.version): no declared license" -ForegroundColor Yellow
                $summary += "SKIP $name -> $($latest.version) (no declared license)"
                continue
            }
        } elseif ($curLic -and $curLic -ne $latestLic) {
            Write-Host "SKIP $name ${cur} -> $($latest.version): license changed ($curLic -> $latestLic)" -ForegroundColor Yellow
            $summary += "SKIP $name -> $($latest.version) (license $curLic -> $latestLic)"
            continue
        } elseif ($latestLic -notin $AllowedLicenses) {
            Write-Host "SKIP $name ${cur} -> $($latest.version): license $latestLic not in allowlist" -ForegroundColor Yellow
            $summary += "SKIP $name -> $($latest.version) (license $latestLic not allowed)"
            continue
        }

        Write-Host "BUMP $name ${cur} -> $($latest.version) ($latestLic) in $($file.Name)" -ForegroundColor Green
        $summary += "BUMP $name $cur -> $($latest.version) ($latestLic)"
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

if ($GithubOutput -and $env:GITHUB_OUTPUT) {
    "bumped=$($bumpedAny.ToString().ToLower())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    $sumPath = Join-Path ([System.IO.Path]::GetTempPath()) "bump-summary.txt"
    $summary | Out-File -FilePath $sumPath -Encoding utf8
    "summary-file=$sumPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if ($DryRun) { Write-Host "(dry run — no files modified)" -ForegroundColor Gray }
