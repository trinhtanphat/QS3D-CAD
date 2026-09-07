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

    Write-Host '== AutoCAD native qualification handoff matrix smoke =='
    $generations = @('2021','2022','2023','2024','2025','2026','2027')
    foreach ($generation in $generations) {
        $arguments = @(
            '-NoProfile', '-File', 'scripts/invoke-autocad-native-qualification-handoff.ps1',
            '-HostGeneration', $generation
        )
        if ($generation -eq '2026') { $arguments += '-DryRunBootstrapper' }
        Invoke-CheckedNative "AutoCAD $generation native qualification handoff smoke" 'pwsh' $arguments
    }

    Write-Host '== AutoCAD native qualification handoff mismatch rejection smoke =='
    $tempManifest = Join-Path ([IO.Path]::GetTempPath()) ("qs3d-family-engineering-mismatch-{0}.json" -f [Guid]::NewGuid().ToString('N'))
    try {
        $engineering = Get-Content -Raw -LiteralPath 'installer/product-family.engineering.manifest.json' | ConvertFrom-Json -Depth 32
        $target = @($engineering.components | Where-Object { $_.product -eq 'autocad' -and $_.hostGeneration -eq '2026' })
        if ($target.Count -ne 1) { throw 'Mismatch smoke could not resolve the AutoCAD 2026 engineering component.' }
        $target[0].releaseTag = 'test-v0.1.0-ci.999'
        $engineering | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $tempManifest -Encoding utf8

        & pwsh -NoProfile -File 'scripts/invoke-autocad-native-qualification-handoff.ps1' -HostGeneration '2026' -ManifestPath $tempManifest
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            throw 'AutoCAD native qualification handoff mismatch smoke unexpectedly succeeded.'
        }
        Write-Host "Expected mismatch rejection observed with exit code $exitCode."
        # The child failure is the expected assertion outcome; do not leak it as this validator's process status.
        $global:LASTEXITCODE = 0
    }
    finally {
        Remove-Item -LiteralPath $tempManifest -Force -ErrorAction SilentlyContinue
    }

    Write-Host 'QS3D product-family bootstrapper validation PASS'
}
finally {
    Pop-Location
}
