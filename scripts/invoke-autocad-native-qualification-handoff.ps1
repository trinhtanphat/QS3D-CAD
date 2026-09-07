[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string]$HostGeneration,
    [string]$AutoCADRepositoryPath,
    [switch]$DryRunBootstrapper
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'installer\product-family.engineering.manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Engineering family manifest was not found: $manifestPath"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 32
$matches = @($manifest.components | Where-Object {
    [string]$_.product -eq 'autocad' -and [string]$_.hostGeneration -eq $HostGeneration
})
if ($matches.Count -ne 1) {
    throw "Engineering manifest must contain exactly one AutoCAD $HostGeneration component; found $($matches.Count)."
}
$component = $matches[0]
if ($component.enabled -ne $true) {
    throw "Engineering AutoCAD $HostGeneration component must be enabled for native-test planning."
}

$qualification = ([string]$component.qualification).ToLowerInvariant()
if (-not $qualification.Contains('engineering') -or -not $qualification.Contains('not-native-pass')) {
    throw "Engineering AutoCAD $HostGeneration qualification must explicitly remain engineering/not-native-pass."
}

$expected = [ordered]@{
    repository = 'trinhtanphat/QS3D-AutoCAD'
    releaseTag = 'test-v0.1.0-ci.266'
    sourceSha = '8dd65a7e5061430f76e027467261beb8f18c69a8'
    assetName = 'QS3D-AutoCAD-0.0.0-ci-Setup.exe'
    downloadUrl = 'https://github.com/trinhtanphat/QS3D-AutoCAD/releases/download/test-v0.1.0-ci.266/QS3D-AutoCAD-0.0.0-ci-Setup.exe'
    sha256 = '9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de'
    bytes = [int64]67998359
    packageKind = 'exe'
    installStrategy = 'execute'
}
foreach ($key in $expected.Keys) {
    $actual = $component.$key
    if ($key -eq 'bytes') {
        if ([int64]$actual -ne [int64]$expected[$key]) {
            throw "Engineering AutoCAD $HostGeneration $key drifted from CI #266. Expected $($expected[$key]); actual $actual."
        }
    }
    elseif ([string]$actual -ne [string]$expected[$key]) {
        throw "Engineering AutoCAD $HostGeneration $key drifted from CI #266. Expected '$($expected[$key])'; actual '$actual'."
    }
}

if ($DryRunBootstrapper) {
    $packagedExe = Join-Path $root 'artifacts\family-bootstrapper\QS3D-Family-Setup-win-x64.exe'
    if (Test-Path -LiteralPath $packagedExe -PathType Leaf) {
        & $packagedExe --manifest $manifestPath --dry-run
        if ($LASTEXITCODE -ne 0) {
            throw "Packaged family bootstrapper manifest-wide dry-run failed with exit code $LASTEXITCODE."
        }
    }
    else {
        $project = Join-Path $root 'tools\QS3D.ProductBootstrapper\QS3D.ProductBootstrapper.csproj'
        & dotnet run --project $project -c Release -- --manifest $manifestPath --dry-run
        if ($LASTEXITCODE -ne 0) {
            throw "Family bootstrapper manifest-wide dry-run failed with exit code $LASTEXITCODE."
        }
    }
}

$requiredNativeScripts = @(
    'scripts\new-native-acceptance.ps1',
    'scripts\record-native-runtime.ps1',
    'scripts\record-native-result.ps1',
    'scripts\validate-native-acceptance.ps1'
)
$autoCadRoot = '<QS3D-AutoCAD>'
if (-not [string]::IsNullOrWhiteSpace($AutoCADRepositoryPath)) {
    if (-not (Test-Path -LiteralPath $AutoCADRepositoryPath -PathType Container)) {
        throw "QS3D-AutoCAD checkout directory was not found: $AutoCADRepositoryPath"
    }
    $autoCadRoot = (Resolve-Path -LiteralPath $AutoCADRepositoryPath).Path
    foreach ($relative in $requiredNativeScripts) {
        $candidate = Join-Path $autoCadRoot $relative
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "QS3D-AutoCAD native qualification checkout is missing '$relative'."
        }
    }
}

$nativeVersion = '0.0.0-ci'
$evidenceRelative = "artifacts/native-acceptance/AutoCAD-$HostGeneration.json"
$runtimeExpectation = switch ($HostGeneration) {
    { $_ -in @('2021','2022','2023','2024') } { 'CLR major 4'; break }
    '2025' { 'CLR major 8'; break }
    '2026' { 'CLR major 8 or 10'; break }
    '2027' { 'CLR major 10'; break }
    default { throw "Unsupported AutoCAD generation: $HostGeneration" }
}

Write-Host ''
Write-Host "QS3D AutoCAD $HostGeneration native qualification handoff READY"
Write-Host "Engineering release: $($expected.releaseTag)"
Write-Host "Source SHA: $($expected.sourceSha)"
Write-Host "Setup asset: $($expected.assetName)"
Write-Host "Setup SHA-256: $($expected.sha256)"
Write-Host "Setup bytes: $($expected.bytes)"
Write-Host "Expected runtime identity after QS3D loads: $runtimeExpectation"
Write-Host 'State: engineering candidate, unsigned, NOT NATIVE PASS, NOT production qualified.'
Write-Host ''
Write-Host 'Before creating native evidence, download the exact CI #266 release assets into the QS3D-AutoCAD artifacts directory:'
Write-Host '  QS3D-AutoCAD-0.0.0-ci.zip'
Write-Host '  QS3D-AutoCAD-0.0.0-ci-Setup.exe'
Write-Host '  RELEASE-PROVENANCE.json'
Write-Host '  SHA256SUMS.txt'
Write-Host 'Release: https://github.com/trinhtanphat/QS3D-AutoCAD/releases/tag/test-v0.1.0-ci.266'
Write-Host ''
Write-Host 'Run on the licensed test machine from the QS3D-AutoCAD checkout:'
Write-Host "  Set-Location '$autoCadRoot'"
Write-Host "  ./scripts/new-native-acceptance.ps1 -Version '$nativeVersion' -HostGeneration '$HostGeneration' -AcadExe '<absolute-path-to-acad.exe>' -Operator '<operator-name>'"
Write-Host "  # Launch AutoCAD fresh, do not NETLOAD manually, load QS3D normally, then run QS3DABOUT. Record the observed $runtimeExpectation value:"
Write-Host "  ./scripts/record-native-runtime.ps1 -EvidencePath '$evidenceRelative' -ObservedClrVersion '<exact-QS3DABOUT-CLR-version>' -Notes '<observed runtime identity>'"
Write-Host '  # Execute every native check in native-acceptance/required-checks.json. Record only what was actually observed:'
Write-Host "  ./scripts/record-native-result.ps1 -EvidencePath '$evidenceRelative' -CheckId '<required-check-id>' -Status pass -Notes '<specific observed evidence>'"
Write-Host '  # Use -Status fail or -Status blocked when that is what the real host shows. This handoff never records PASS automatically.'

if ($HostGeneration -in @('2021','2022','2023','2024')) {
    Write-Host ''
    Write-Host "Legacy host validation for this generation only (does not qualify sibling R24.x hosts):"
    Write-Host "  ./scripts/validate-native-acceptance.ps1 -Version '$nativeVersion' -RequiredGenerations @('$HostGeneration')"
}
else {
    Write-Host ''
    Write-Host 'Modern production matrix requires distinct real-host sessions for AutoCAD 2025, 2026 and 2027 against the same candidate.'
    Write-Host "After all three sessions are complete: ./scripts/validate-native-acceptance.ps1 -Version '$nativeVersion'"
}

Write-Host ''
Write-Host 'Handoff validation PASS — no native PASS evidence was created or modified.'
