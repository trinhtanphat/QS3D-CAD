$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host '== QS3D-CAD preflight =='
python scripts/preflight.py

Write-Host '== Initialize pinned QS3D-Platform =='
git submodule update --init --recursive

Write-Host '== QS3D-Platform preflight =='
python external/QS3D-Platform/scripts/preflight.py

Write-Host '== Build QS3D-Platform Release =='
dotnet build external/QS3D-Platform/QS3D.Platform.sln -c Release

Write-Host '== Run QS3D-Platform deterministic smoke =='
dotnet run --project external/QS3D-Platform/tests/QS3D.Platform.SmokeTests/QS3D.Platform.SmokeTests.csproj -c Release --no-build

Write-Host '== Build standalone host Release =='
dotnet build src/QS3D.Cad.Host/QS3D.Cad.Host.csproj -c Release

Write-Host '== Run standalone deterministic smoke =='
dotnet run --project tests/QS3D.Cad.SmokeTests/QS3D.Cad.SmokeTests.csproj -c Release

Write-Host '== Build desktop shell Release (Windows) =='
dotnet build src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj -c Release

Write-Host 'QS3D-CAD validation PASS'
