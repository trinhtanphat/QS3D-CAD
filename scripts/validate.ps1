$ErrorActionPreference = 'Stop'

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Step failed with exit code $exitCode."
    }
}

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host '== QS3D-CAD preflight =='
Invoke-CheckedNative 'QS3D-CAD preflight' 'python' @('scripts/preflight.py')

Write-Host '== QS3D-CAD source boundary =='
Invoke-CheckedNative 'QS3D-CAD source boundary' 'python' @('scripts/check-standalone-source-boundary.py')

Write-Host '== QS3D-CAD release contract =='
Invoke-CheckedNative 'QS3D-CAD release contract' 'python' @('scripts/check-release-contract.py')

Write-Host '== QS3D-CAD desktop UI contract =='
Invoke-CheckedNative 'QS3D-CAD desktop UI contract' 'python' @('scripts/check-desktop-ui-contract.py')

Write-Host '== Initialize pinned QS3D-Platform =='
Invoke-CheckedNative 'Initialize pinned QS3D-Platform' 'git' @('submodule', 'update', '--init', '--recursive')

Write-Host '== Verify exact Platform checkout =='
Invoke-CheckedNative 'Verify exact Platform checkout' 'python' @('scripts/check-platform-pin.py')

Write-Host '== QS3D-CAD Cubicost parity boundary =='
Invoke-CheckedNative 'QS3D-CAD Cubicost parity boundary' 'python' @('scripts/check-cubicost-parity.py')

Write-Host '== QS3D-Platform preflight =='
Invoke-CheckedNative 'QS3D-Platform preflight' 'python' @('external/QS3D-Platform/scripts/preflight.py')

Write-Host '== QS3D-Platform netstandard2.0 boundary =='
Invoke-CheckedNative 'QS3D-Platform netstandard2.0 boundary' 'python' @('external/QS3D-Platform/scripts/check-netstandard20-boundary.py')

Write-Host '== QS3D-Platform reference services gate =='
Invoke-CheckedNative 'QS3D-Platform reference services gate' 'python' @('external/QS3D-Platform/scripts/check-reference-services.py')

Write-Host '== QS3D-Platform parity gate =='
Invoke-CheckedNative 'QS3D-Platform parity gate' 'python' @('external/QS3D-Platform/scripts/check-parity.py')

Write-Host '== QS3D-Platform family schema gate =='
Invoke-CheckedNative 'QS3D-Platform family schema gate' 'python' @('external/QS3D-Platform/scripts/check-families.py')

Write-Host '== Build QS3D-Platform Release =='
Invoke-CheckedNative 'Build QS3D-Platform Release' 'dotnet' @('build', 'external/QS3D-Platform/QS3D.Platform.sln', '-c', 'Release')

Write-Host '== Run QS3D-Platform deterministic smoke =='
Invoke-CheckedNative 'Run QS3D-Platform deterministic smoke' 'dotnet' @('run', '--project', 'external/QS3D-Platform/tests/QS3D.Platform.SmokeTests/QS3D.Platform.SmokeTests.csproj', '-c', 'Release', '--no-build')

Write-Host '== Build standalone host Release =='
Invoke-CheckedNative 'Build standalone host Release' 'dotnet' @('build', 'src/QS3D.Cad.Host/QS3D.Cad.Host.csproj', '-c', 'Release')

Write-Host '== Run standalone deterministic smoke =='
Invoke-CheckedNative 'Run standalone deterministic smoke' 'dotnet' @('run', '--project', 'tests/QS3D.Cad.SmokeTests/QS3D.Cad.SmokeTests.csproj', '-c', 'Release')

Write-Host '== Build desktop shell Release (Windows) =='
Invoke-CheckedNative 'Build desktop shell Release (Windows)' 'dotnet' @('build', 'src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj', '-c', 'Release')

Write-Host 'QS3D-CAD validation PASS'
