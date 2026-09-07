$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )
    & $FilePath @ArgumentList
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { throw "$Step failed with exit code $exitCode." }
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host '== QS3D product-family bootstrapper boundary =='
    Invoke-CheckedNative 'QS3D product-family bootstrapper boundary' 'python' @('scripts/check-product-family-bootstrapper.py')

    Write-Host '== Build product-family bootstrapper Release =='
    Invoke-CheckedNative 'Build product-family bootstrapper Release' 'dotnet' @('build', 'tools/QS3D.ProductBootstrapper/QS3D.ProductBootstrapper.csproj', '-c', 'Release')

    Write-Host '== Run product-family bootstrapper deterministic smoke =='
    Invoke-CheckedNative 'Run product-family bootstrapper deterministic smoke' 'dotnet' @('run', '--project', 'tests/QS3D.ProductBootstrapper.SmokeTests/QS3D.ProductBootstrapper.SmokeTests.csproj', '-c', 'Release')

    Write-Host '== AutoCAD native qualification handoff smoke =='
    Invoke-CheckedNative 'AutoCAD native qualification handoff smoke' 'pwsh' @(
        '-NoProfile', '-File', 'scripts/invoke-autocad-native-qualification-handoff.ps1',
        '-HostGeneration', '2026', '-DryRunBootstrapper'
    )

    Write-Host 'QS3D product-family bootstrapper validation PASS'
}
finally {
    Pop-Location
}
