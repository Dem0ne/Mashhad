<#
.SYNOPSIS
    Builds a self-contained Mashhad into .\publish and (optionally) the installer into .\dist.

.EXAMPLE
    pwsh scripts/build.ps1                # publish\Mashhad\Mashhad.exe
    pwsh scripts/build.ps1 -Installer     # + dist\Mashhad-Setup-<version>.exe (needs Inno Setup 6)
    pwsh scripts/build.ps1 -Zip           # + dist\Mashhad-<version>-win-x64.zip (portable)
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Installer,
    [switch]$Zip,
    [switch]$SkipDeps
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $SkipDeps) { & (Join-Path $PSScriptRoot 'fetch-deps.ps1') }

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
$publish = Join-Path $root 'publish\Mashhad'
$dist    = Join-Path $root 'dist'
Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dist | Out-Null

# Thmanyah Sans is compiled in (encrypted) only when MASHHAD_FONT_KEY is set; otherwise Segoe UI Variable.
& (Join-Path $PSScriptRoot 'embed-font-key.ps1')
try {
    dotnet publish src/Mashhad.App/Mashhad.App.csproj -c $Configuration -r win-x64 -p:Platform=x64 --self-contained true -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}
finally { & (Join-Path $PSScriptRoot 'embed-font-key.ps1') -Remove }
$plainFonts = Get-ChildItem $publish -Recurse -Include '*.otf', '*.ttf', '*.enc' -File
if ($plainFonts) { throw "Font files must not ship as files: $($plainFonts.Name -join ', ')" }
Get-ChildItem $publish -Recurse -Filter '*.pdb' | Remove-Item -Force

foreach ($required in 'Mashhad.exe', 'mpv\libmpv-2.dll', 'ffmpeg\ffmpeg.exe', 'ffmpeg\ffprobe.exe') {
    if (-not (Test-Path (Join-Path $publish $required))) { Write-Warning "Missing from the build output: $required" }
}
Write-Host "Published to $publish"

if ($Zip) {
    $zipPath = Join-Path $dist "Mashhad-$version-win-x64.zip"
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath
    Write-Host "Portable zip: $zipPath"
}

if ($Installer) {
    $iscc = $null
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
    if (-not $iscc) {
        foreach ($candidate in "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") {
            if (Test-Path $candidate) { $iscc = $candidate; break }
        }
    }
    if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php' }
    & $iscc "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$dist" (Join-Path $root 'installer\Mashhad.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
    Write-Host "Installer: $dist\Mashhad-Setup-$version.exe"
}
