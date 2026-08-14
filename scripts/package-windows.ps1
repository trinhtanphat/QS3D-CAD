[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$IsccPath = "",
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
    }

    if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<prerelease>[0-9A-Za-z.-]+))?$') {
        throw "VERSION must be SemVer-like (for example 0.1.0-preview.1). Got '$Version'."
    }

    $major = [int]$Matches.major
    $minor = [int]$Matches.minor
    $patch = [int]$Matches.patch
    $prerelease = $Matches.prerelease
    $revision = 0
    if (-not [string]::IsNullOrWhiteSpace($prerelease) -and $prerelease -match '(?:^|\.)(\d+)$') {
        $revision = [int]$Matches[1]
    }
    foreach ($component in @($major, $minor, $patch, $revision)) {
        if ($component -lt 0 -or $component -gt 65535) {
            throw "Windows file-version components must be between 0 and 65535."
        }
    }
    $fileVersion = "$major.$minor.$patch.$revision"

    if (-not $SkipValidation) {
        & (Join-Path $root "scripts\validate.ps1")
        if ($LASTEXITCODE -ne 0) { throw "QS3D CAD validation failed with exit code $LASTEXITCODE." }
    }

    $publishDir = Join-Path $root "artifacts\publish\$Runtime"
    $installerDir = Join-Path $root "artifacts\installer"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    if (Test-Path $installerDir) { Remove-Item $installerDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

    dotnet publish "src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj" `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:Version=$Version `
        -p:FileVersion=$fileVersion `
        -p:InformationalVersion=$Version `
        -p:PublishReadyToRun=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

    $application = Join-Path $publishDir "QS3D.CAD.exe"
    if (-not (Test-Path $application)) {
        throw "Expected published executable was not produced: $application"
    }

    if ([string]::IsNullOrWhiteSpace($IsccPath)) {
        $candidates = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($IsccPath)) {
            $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
            if ($null -ne $command) { $IsccPath = $command.Source }
        }
    }

    if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path $IsccPath)) {
        throw "Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -IsccPath."
    }

    $iss = Join-Path $root "installer\QS3D-CAD.iss"
    & $IsccPath "/DAppVersion=$Version" "/DFileVersion=$fileVersion" "/DSourceDir=$publishDir" "/DOutputDir=$installerDir" $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE." }

    $installer = Join-Path $installerDir "QS3D-CAD-Setup-win-x64.exe"
    if (-not (Test-Path $installer)) {
        throw "Expected installer was not produced: $installer"
    }

    $hash = (Get-FileHash -Algorithm SHA256 $installer).Hash.ToLowerInvariant()
    $checksum = Join-Path $installerDir "QS3D-CAD-Setup-win-x64.exe.sha256"
    Set-Content -Path $checksum -Value "$hash  QS3D-CAD-Setup-win-x64.exe" -Encoding ascii

    Write-Host "QS3D CAD Windows package PASS"
    Write-Host "Version: $Version"
    Write-Host "File version: $fileVersion"
    Write-Host "Installer: $installer"
    Write-Host "SHA256: $hash"
}
finally {
    Pop-Location
}
