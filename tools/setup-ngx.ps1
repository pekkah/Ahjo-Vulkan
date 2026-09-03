#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fetch the pinned NVIDIA DLSS (NGX) SDK pieces needed to develop and run
    Ahjo.Vulkan.Ngx locally.

.DESCRIPTION
    Downloads individual files from the public NVIDIA/DLSS GitHub repository at
    the tag pinned as <NgxVersion> in Directory.Build.props, verifies each one
    against native/ngx/pins.sha256, and stages them under native/ngx/:

        native/ngx/include/                 SDK headers (generator input of record)
        native/ngx/NGX-LICENSE.txt          NVIDIA RTX SDKs licence text
        native/ngx/staged/<rid>/            static client lib(s) the shim links
        native/ngx/staged/<rid>/rel/        production feature DLL  (nvngx_dlss.dll / libnvidia-ngx-dlss.so.*)
        native/ngx/staged/<rid>/dev/        development feature DLL (debug overlay; NEVER ship)
        native/ngx/doc/                     programming guide PDF (-IncludeDocs)

    Everything under downloaded/, staged/ and doc/ is git-ignored. The feature
    DLL is deliberately not part of any Ahjo package: consumers supply it
    themselves (see issue #214). This script only serves local development,
    the samples, and the shim build.

    The whole repository is >600 MB of binaries, so nothing is cloned; each
    file is fetched by raw URL and pinned by SHA-256.

.PARAMETER Version
    NVIDIA/DLSS tag to fetch, e.g. v310.7.0. Defaults to <NgxVersion> in
    Directory.Build.props.

.PARAMETER Platform
    Which feature/static libraries to fetch: host (default), win-x64, linux-x64,
    or all. Headers and the licence are always fetched.

.PARAMETER IncludeDocs
    Also fetch the DLSS programming guide PDF into native/ngx/doc/.

.PARAMETER UpdatePins
    Bump mode: (re)write native/ngx/pins.sha256 from the downloaded files
    instead of verifying against it. Use together with -Platform all
    -IncludeDocs -Force when moving the pin, then commit the new pins file.

.PARAMETER Force
    Re-download files that are already present in native/ngx/downloaded/.

.EXAMPLE
    ./tools/setup-ngx.ps1
    ./tools/setup-ngx.ps1 -Platform all -IncludeDocs
    ./tools/setup-ngx.ps1 -Version v310.8.0 -Platform all -IncludeDocs -Force -UpdatePins
#>
[CmdletBinding()]
param(
    [string] $Version,
    [ValidateSet('host', 'win-x64', 'linux-x64', 'all')]
    [string] $Platform = 'host',
    [switch] $IncludeDocs,
    [switch] $UpdatePins,
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest's progress bar is very slow on large files

$RepoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$PropsFile = Join-Path $RepoRoot 'Directory.Build.props'
$NgxRoot   = Join-Path $RepoRoot 'native' 'ngx'
$PinsFile  = Join-Path $NgxRoot 'pins.sha256'

# ---------------------------------------------------------------------------
# Version pin
# ---------------------------------------------------------------------------
if (-not $Version) {
    [xml] $props = Get-Content -LiteralPath $PropsFile -Raw
    $node = $props.SelectSingleNode('//NgxVersion')
    if (-not $node -or -not $node.InnerText.Trim()) {
        throw "No <NgxVersion> in $PropsFile and no -Version given."
    }
    $Version = $node.InnerText.Trim()
}
if ($Version -notmatch '^v\d+\.\d+\.\d+$') {
    throw "Version '$Version' does not look like an NVIDIA/DLSS tag (expected e.g. v310.7.0)."
}
$Bare    = $Version.TrimStart('v')
$BaseUrl = "https://raw.githubusercontent.com/NVIDIA/DLSS/$Version"

# ---------------------------------------------------------------------------
# File manifest: upstream repo path -> path under native/ngx/ (relative)
# ---------------------------------------------------------------------------
$Headers = @(
    'nvsdk_ngx.h', 'nvsdk_ngx_defs.h', 'nvsdk_ngx_params.h', 'nvsdk_ngx_helpers.h',
    'nvsdk_ngx_vk.h', 'nvsdk_ngx_defs_vk.h', 'nvsdk_ngx_helpers_vk.h',
    # Ray Reconstruction + Frame Generation headers: not bound yet (#214 "Later"),
    # pinned now so a later phase does not need a second pin bump.
    'nvsdk_ngx_defs_dlssd.h', 'nvsdk_ngx_helpers_dlssd.h', 'nvsdk_ngx_helpers_dlssd_vk.h', 'nvsdk_ngx_params_dlssd.h',
    'nvsdk_ngx_defs_dlssg.h', 'nvsdk_ngx_helpers_dlssg.h', 'nvsdk_ngx_helpers_dlssg_vk.h', 'nvsdk_ngx_params_dlssg.h'
)

$Files = [System.Collections.Generic.List[hashtable]]::new()
$Files.Add(@{ Path = 'LICENSE.txt'; Stage = 'NGX-LICENSE.txt' })
foreach ($h in $Headers) { $Files.Add(@{ Path = "include/$h"; Stage = "include/$h" }) }

$WinFiles = @(
    # /MT static client library (+ debug-CRT variant). The shim links this.
    @{ Path = 'lib/Windows_x86_64/x64/nvsdk_ngx_s.lib';     Stage = 'staged/win-x64/nvsdk_ngx_s.lib' }
    @{ Path = 'lib/Windows_x86_64/x64/nvsdk_ngx_s_dbg.lib'; Stage = 'staged/win-x64/nvsdk_ngx_s_dbg.lib' }
    # Feature DLL: rel = production, dev = watermarked debug overlay build.
    @{ Path = 'lib/Windows_x86_64/rel/nvngx_dlss.dll';      Stage = 'staged/win-x64/rel/nvngx_dlss.dll' }
    @{ Path = 'lib/Windows_x86_64/dev/nvngx_dlss.dll';      Stage = 'staged/win-x64/dev/nvngx_dlss.dll' }
)
$LinuxFiles = @(
    @{ Path = 'lib/Linux_x86_64/libnvsdk_ngx.a';                       Stage = 'staged/linux-x64/libnvsdk_ngx.a' }
    @{ Path = "lib/Linux_x86_64/rel/libnvidia-ngx-dlss.so.$Bare";      Stage = "staged/linux-x64/rel/libnvidia-ngx-dlss.so.$Bare" }
    @{ Path = "lib/Linux_x86_64/dev/libnvidia-ngx-dlss.so.$Bare";      Stage = "staged/linux-x64/dev/libnvidia-ngx-dlss.so.$Bare" }
)
$DocFiles = @(
    @{ Path = 'doc/DLSS_Programming_Guide_Release.pdf'; Stage = 'doc/DLSS_Programming_Guide_Release.pdf' }
)

$isWin = ($PSVersionTable.PSVersion.Major -lt 6) -or $IsWindows
$wantWin   = $Platform -eq 'all' -or $Platform -eq 'win-x64'   -or ($Platform -eq 'host' -and $isWin)
$wantLinux = $Platform -eq 'all' -or $Platform -eq 'linux-x64' -or ($Platform -eq 'host' -and -not $isWin)
if ($wantWin)      { foreach ($f in $WinFiles)   { $Files.Add($f) } }
if ($wantLinux)    { foreach ($f in $LinuxFiles) { $Files.Add($f) } }
if ($IncludeDocs)  { foreach ($f in $DocFiles)   { $Files.Add($f) } }

# ---------------------------------------------------------------------------
# Pins
# ---------------------------------------------------------------------------
$pins = @{}
$pinnedTag = $null
if (Test-Path -LiteralPath $PinsFile) {
    foreach ($line in Get-Content -LiteralPath $PinsFile) {
        if ($line -match '^#\s*tag:\s*(\S+)')      { $pinnedTag = $Matches[1]; continue }
        if ($line -match '^\s*(#|$)')              { continue }
        if ($line -match '^([0-9a-fA-F]{64})\s+(.+?)\s*$') { $pins[$Matches[2]] = $Matches[1].ToLowerInvariant(); continue }
        throw "Unparseable line in ${PinsFile}: $line"
    }
}
if (-not $UpdatePins) {
    if (-not $pinnedTag) {
        throw "$PinsFile is missing or has no tag line. Run with -UpdatePins to create it."
    }
    if ($pinnedTag -ne $Version) {
        throw "pins.sha256 is for $pinnedTag but $Version was requested. Bump with: ./tools/setup-ngx.ps1 -Version $Version -Platform all -IncludeDocs -Force -UpdatePins"
    }
}

# ---------------------------------------------------------------------------
# Download + verify + stage
# ---------------------------------------------------------------------------
function Get-Sha256([string] $path) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-Download([string] $url, [string] $dest) {
    $dir = Split-Path -Parent $dest
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $tmp = "$dest.partial"
    $attempt = 0
    while ($true) {
        $attempt++
        try {
            Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
            Move-Item -LiteralPath $tmp -Destination $dest -Force
            return
        }
        catch {
            if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
            if ($attempt -ge 3) { throw "Download failed after $attempt attempts: $url`n$($_.Exception.Message)" }
            Write-Warning "Download failed (attempt $attempt): $url — retrying"
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

$downloadRoot = Join-Path $NgxRoot 'downloaded' $Version
$newPins = [ordered]@{}
$failures = @()
$staged = @()

Write-Host "NVIDIA DLSS SDK $Version  ->  $NgxRoot" -ForegroundColor Cyan
Write-Host "  platform: $Platform  (win-x64: $wantWin, linux-x64: $wantLinux)  docs: $IncludeDocs  mode: $(if ($UpdatePins) { 'update pins' } else { 'verify' })"

foreach ($f in $Files) {
    $relPath = $f.Path
    $url     = "$BaseUrl/$relPath"
    $local   = Join-Path $downloadRoot ($relPath -replace '/', [IO.Path]::DirectorySeparatorChar)

    if ($Force -or -not (Test-Path -LiteralPath $local)) {
        Write-Host "  fetch  $relPath"
        Invoke-Download -url $url -dest $local
    }
    else {
        Write-Host "  cached $relPath"
    }

    $hash = Get-Sha256 $local
    if ($UpdatePins) {
        $newPins[$relPath] = $hash
    }
    elseif (-not $pins.ContainsKey($relPath)) {
        $failures += "$relPath is not in pins.sha256 (sha256 $hash). Re-run with -UpdatePins if this file is newly required."
        continue
    }
    elseif ($pins[$relPath] -ne $hash) {
        Remove-Item -LiteralPath $local -Force
        $failures += "$relPath SHA-256 mismatch: expected $($pins[$relPath]), got $hash. File deleted; upstream tag content changed or the download was corrupted."
        continue
    }

    $dest = Join-Path $NgxRoot ($f.Stage -replace '/', [IO.Path]::DirectorySeparatorChar)
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path -LiteralPath $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
    Copy-Item -LiteralPath $local -Destination $dest -Force
    $staged += $f.Stage
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
    throw "$($failures.Count) file(s) failed verification. Nothing from those files was staged."
}

if ($UpdatePins) {
    # Merge: keep pins for files not fetched this run (e.g. the other platform),
    # overwrite the ones we have. Drop everything if the tag changed.
    $merged = [ordered]@{}
    if ($pinnedTag -eq $Version) {
        foreach ($k in ($pins.Keys | Sort-Object)) { $merged[$k] = $pins[$k] }
    }
    foreach ($k in $newPins.Keys) { $merged[$k] = $newPins[$k] }

    $lines = @(
        "# SHA-256 pins for the NVIDIA DLSS SDK files fetched by tools/setup-ngx.ps1.",
        "# Regenerate with: ./tools/setup-ngx.ps1 -Version <tag> -Platform all -IncludeDocs -Force -UpdatePins",
        "# tag: $Version"
    )
    foreach ($k in ($merged.Keys | Sort-Object)) { $lines += "$($merged[$k])  $k" }
    Set-Content -LiteralPath $PinsFile -Value $lines -Encoding utf8NoBOM
    Write-Host "  wrote  $PinsFile ($($merged.Count) entries)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Staged under native/ngx/:" -ForegroundColor Green
$staged | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host "Feature DLL for running samples/tests locally (not part of any package; see #214):"
if ($wantWin)   { Write-Host "  $(Join-Path $NgxRoot 'staged' 'win-x64'   'rel' 'nvngx_dlss.dll')" }
if ($wantLinux) { Write-Host "  $(Join-Path $NgxRoot 'staged' 'linux-x64' 'rel' "libnvidia-ngx-dlss.so.$Bare")" }
Write-Host "  dev/ builds carry the on-screen debug overlay and must never ship."
