[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $SkipValidation) {
        & (Join-Path $root "scripts\validate.ps1")
        if ($LASTEXITCODE -ne 0) { throw "QS3D CAD validation failed with exit code $LASTEXITCODE." }
        & (Join-Path $root "scripts\validate-family-bootstrapper.ps1")
        if ($LASTEXITCODE -ne 0) { throw "QS3D family bootstrapper validation failed with exit code $LASTEXITCODE." }
    }

    $publishDir = Join-Path $root "artifacts\family-bootstrapper\publish"
    $outputDir = Join-Path $root "artifacts\family-bootstrapper"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $publishDir, $outputDir | Out-Null

    dotnet publish "tools/QS3D.ProductBootstrapper/QS3D.ProductBootstrapper.csproj" `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Product-family bootstrapper publish failed with exit code $LASTEXITCODE." }

    $publishedExe = Join-Path $publishDir "QS3D.ProductBootstrapper.exe"
    if (-not (Test-Path $publishedExe)) { throw "Published bootstrapper executable was not produced." }

    $exe = Join-Path $outputDir "QS3D-Family-Setup-win-x64.exe"
    $manifestSource = Join-Path $root "installer\product-family.manifest.json"
    $engineeringManifestSource = Join-Path $root "installer\product-family.engineering.manifest.json"
    $manifest = Join-Path $outputDir "product-family.manifest.json"
    $engineeringManifest = Join-Path $outputDir "product-family.engineering.manifest.json"
    Copy-Item -LiteralPath $publishedExe -Destination $exe -Force
    Copy-Item -LiteralPath $manifestSource -Destination $manifest -Force
    Copy-Item -LiteralPath $engineeringManifestSource -Destination $engineeringManifest -Force

    foreach ($path in @($exe, $manifest, $engineeringManifest)) {
        $hash = (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
        $sidecar = "$path.sha256"
        Set-Content -LiteralPath $sidecar -Value "$hash  $([IO.Path]::GetFileName($path))" -Encoding ascii
    }

    & $exe --manifest $manifest --dry-run
    if ($LASTEXITCODE -ne 0) { throw "Published bootstrapper production-manifest dry-run failed with exit code $LASTEXITCODE." }

    & $exe --manifest $engineeringManifest --dry-run
    if ($LASTEXITCODE -ne 0) { throw "Published bootstrapper engineering-manifest dry-run failed with exit code $LASTEXITCODE." }

    Write-Host "QS3D family bootstrapper package PASS"
    Write-Host "Executable: $exe"
    Write-Host "Production manifest: $manifest"
    Write-Host "Engineering manifest: $engineeringManifest"
}
finally {
    Pop-Location
}
