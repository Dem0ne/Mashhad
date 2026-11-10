<#
.SYNOPSIS
    Downloads the native engines Mashhad needs into .\native (not committed to git):
      native\mpv\libmpv-2.dll          - playback/display engine (mpv, shinchiro Windows builds)
      native\ffmpeg\ffmpeg.exe, ffprobe.exe + DLLs - editing/export (FFmpeg, BtbN LGPL shared builds)

.DESCRIPTION
    The app project copies native\mpv and native\ffmpeg next to Mashhad.exe on build.
    Re-run with -Force to refresh. You can also drop your own builds into those folders.

.EXAMPLE
    pwsh scripts/fetch-deps.ps1
#>
[CmdletBinding()]
param(
    [switch]$Force,
    # Override to pin exact builds, e.g. for reproducible releases.
    [string]$MpvUrl,
    [string]$FfmpegUrl
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root   = Split-Path -Parent $PSScriptRoot
$native = Join-Path $root 'native'
$mpvDir = Join-Path $native 'mpv'
$ffDir  = Join-Path $native 'ffmpeg'
$tmp    = Join-Path $native '_download'
New-Item -ItemType Directory -Force -Path $mpvDir, $ffDir, $tmp | Out-Null

$headers = @{ 'User-Agent' = 'mashhad-fetch-deps' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $($env:GITHUB_TOKEN)" }

function Get-LatestAsset([string]$repo, [string]$pattern) {
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases?per_page=10" -Headers $headers
    foreach ($release in $releases) {
        $asset = $release.assets | Where-Object { $_.name -match $pattern } | Sort-Object name -Descending | Select-Object -First 1
        if ($asset) { return $asset.browser_download_url }
    }
    throw "No release asset matching '$pattern' found in $repo."
}

function Expand-Any([string]$archive, [string]$destination) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    if ($archive -like '*.zip') {
        Expand-Archive -Path $archive -DestinationPath $destination -Force
        return
    }
    $sevenZip = $null
    $cmd = Get-Command 7z -ErrorAction SilentlyContinue
    if ($cmd) { $sevenZip = $cmd.Source }
    if (-not $sevenZip) {
        foreach ($candidate in "$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe") {
            if (Test-Path $candidate) { $sevenZip = $candidate; break }
        }
    }
    if ($sevenZip) {
        & $sevenZip x $archive "-o$destination" -y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "7z failed to extract $archive" }
    } else {
        # Windows 10+ ships bsdtar, which reads .7z on current builds.
        & tar -xf $archive -C $destination
        if ($LASTEXITCODE -ne 0) { throw "Could not extract $archive. Install 7-Zip (https://www.7-zip.org) and re-run." }
    }
}

# ---- libmpv -------------------------------------------------------------------------------
$mpvDll = Join-Path $mpvDir 'libmpv-2.dll'
if ($Force -or -not (Test-Path $mpvDll)) {
    if (-not $MpvUrl) { $MpvUrl = Get-LatestAsset 'shinchiro/mpv-winbuild-cmake' '^mpv-dev-x86_64-\d{8}-git-[0-9a-f]+\.7z$' }
    Write-Host "libmpv : $MpvUrl"
    $archive = Join-Path $tmp ([IO.Path]::GetFileName(([Uri]$MpvUrl).AbsolutePath))
    Invoke-WebRequest -Uri $MpvUrl -OutFile $archive -Headers @{ 'User-Agent' = 'mashhad-fetch-deps' }
    $out = Join-Path $tmp 'mpv'
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Any $archive $out
    $dll = Get-ChildItem $out -Recurse -Filter 'libmpv-2.dll' | Select-Object -First 1
    if (-not $dll) { throw 'libmpv-2.dll not found in the mpv dev archive.' }
    Copy-Item $dll.FullName $mpvDll -Force
} else {
    Write-Host "libmpv : already present ($mpvDll)"
}

# ---- FFmpeg -------------------------------------------------------------------------------
$ffExe = Join-Path $ffDir 'ffmpeg.exe'
if ($Force -or -not (Test-Path $ffExe)) {
    if (-not $FfmpegUrl) { $FfmpegUrl = Get-LatestAsset 'BtbN/FFmpeg-Builds' '^ffmpeg-n\d+\.\d+-latest-win64-lgpl-shared-\d+\.\d+\.zip$' }
    Write-Host "FFmpeg : $FfmpegUrl"
    $archive = Join-Path $tmp ([IO.Path]::GetFileName(([Uri]$FfmpegUrl).AbsolutePath))
    Invoke-WebRequest -Uri $FfmpegUrl -OutFile $archive -Headers @{ 'User-Agent' = 'mashhad-fetch-deps' }
    $out = Join-Path $tmp 'ffmpeg'
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Any $archive $out
    $bin = Get-ChildItem $out -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    if (-not $bin) { throw 'ffmpeg.exe not found in the FFmpeg archive.' }
    Get-ChildItem $ffDir -File | Remove-Item -Force
    # ffmpeg.exe, ffprobe.exe and the shared av*.dll / sw*.dll they need (ffplay is not used).
    Get-ChildItem $bin.Directory.FullName -File |
        Where-Object { $_.Name -ne 'ffplay.exe' } |
        Copy-Item -Destination $ffDir -Force
    $license = Get-ChildItem $out -Recurse -Filter 'LICENSE*' | Select-Object -First 1
    if ($license) { Copy-Item $license.FullName (Join-Path $ffDir 'FFmpeg-LICENSE.txt') -Force }
} else {
    Write-Host "FFmpeg : already present ($ffExe)"
}

Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'Native dependencies ready:'
Get-ChildItem $mpvDir, $ffDir -File | ForEach-Object { '  {0,-28} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
& $ffExe -hide_banner -version | Select-Object -First 1
