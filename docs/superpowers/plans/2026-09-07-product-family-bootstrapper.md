# QS3D Product-Family Bootstrapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deterministic Windows x64 QS3D family bootstrapper that detects AutoCAD/BricsCAD hosts, selects exact pinned packages from a local manifest, verifies bytes, and invokes safe installation strategies without coupling standalone QS3D-CAD to vendor runtimes.

**Architecture:** A new isolated `tools/QS3D.ProductBootstrapper` project contains manifest validation, host discovery, planning, acquisition and installation orchestration behind injectable interfaces. A dedicated console smoke project tests all behavior without real registry/network/child installers. Existing standalone projects never reference the bootstrapper.

**Tech Stack:** C# / .NET 8, `System.Text.Json`, `Microsoft.Win32.Registry`, `HttpClient`, `System.IO.Compression`, PowerShell packaging, existing Windows GitHub Actions + Inno Setup pipeline.

**Spec:** `docs/superpowers/specs/2026-09-07-product-family-bootstrapper-design.md`

## Global Constraints

- Standalone `QS3D-CAD` must continue to build/run without AutoCAD or BricsCAD.
- No AutoCAD/BricsCAD managed SDK/plugin DLL may be committed to this repository.
- The manifest is local/packaged in v1; no mutable remote manifest fetch.
- Enabled packages require exact repository, tag, source SHA, asset name, byte length, SHA-256 and HTTPS GitHub release URL.
- Maximum component download and total ZIP expansion: 512 MiB.
- Maximum manifest components: 32; maximum ZIP entries: 10,000.
- Dry-run performs no HTTP, process, extraction, registry write or filesystem staging.
- Hosted CI must never claim licensed CAD native PASS.
- AutoCAD integrated package and BricsCAD V26 remain disabled/pending until durable exact releases exist.

---

### Task 1: Manifest contract and fail-closed parser

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/QS3D.ProductBootstrapper.csproj`
- Create: `tools/QS3D.ProductBootstrapper/ManifestModels.cs`
- Create: `tools/QS3D.ProductBootstrapper/ProductFamilyManifestLoader.cs`
- Create: `tools/QS3D.ProductBootstrapper/ProductFamilyManifestValidator.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/QS3D.ProductBootstrapper.SmokeTests.csproj`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/Program.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/ManifestContractSmoke.cs`

**Interfaces:**
- Produces: `ProductFamilyManifest`, `ProductComponent`, `PackageKind`, `InstallStrategy`, `ProductFamilyManifestLoader.Load(string path)`.

- [ ] **Step 1: Write RED manifest smoke** covering valid schema-1 fixture plus rejection of unknown properties, duplicate case-insensitive properties, duplicate IDs, duplicate product/generation, malformed hashes/SHAs, oversized bytes, unsafe URLs and inconsistent package/strategy pairs.

Core assertion shape:

```csharp
Smoke.Throws<InvalidDataException>(() => ProductFamilyManifestLoader.Load(path));
var manifest = ProductFamilyManifestLoader.Load(validPath);
Smoke.Equal(1, manifest.SchemaVersion);
Smoke.Equal("standalone-preview", manifest.Components[0].Id);
```

- [ ] **Step 2: Run** `dotnet run --project tests/QS3D.ProductBootstrapper.SmokeTests/QS3D.ProductBootstrapper.SmokeTests.csproj -c Release` and verify RED because bootstrapper types do not exist.
- [ ] **Step 3: Implement strict DTOs plus a `JsonDocument` schema-shape pass before DTO deserialization.** Property matching is case-insensitive but every logical property appears exactly once; unknown fields fail closed.
- [ ] **Step 4: Validate the exact allowlist** `trinhtanphat/QS3D-AutoCAD`, `trinhtanphat/QS3D-BricsCAD`, `trinhtanphat/QS3D-CAD`; validate release download URL path matches repository/tag/asset.
- [ ] **Step 5: Run smoke GREEN.**
- [ ] **Step 6: Commit** `feat(distribution): add fail-closed family manifest contract #47`.

### Task 2: Read-only host discovery

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/HostDiscovery.cs`
- Create: `tools/QS3D.ProductBootstrapper/WindowsRegistryHostDiscovery.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/HostDiscoverySmoke.cs`

**Interfaces:**
- Produces: `HostInstallation`, `IHostDiscovery.Discover()`, `IRegistryReader.ReadSubKeys(...)`.

- [ ] **Step 1: Write RED tests** for exact AutoCAD mappings `R24.0..R26.0`, BricsCAD V25/V26 prefix mapping, unsupported releases ignored, and duplicate hive/view discoveries normalized.
- [ ] **Step 2: Run smoke and verify RED.**
- [ ] **Step 3: Implement `IRegistryReader` abstraction and Windows production reader** covering LocalMachine/CurrentUser and Registry64/Registry32 read-only views.
- [ ] **Step 4: Normalize records deterministically by product/generation/install-root and sort ordinally.**
- [ ] **Step 5: Run smoke GREEN.**
- [ ] **Step 6: Commit** `feat(distribution): detect supported CAD host generations #47`.

### Task 3: Exact package planner and dry-run boundary

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/InstallPlanning.cs`
- Create: `tools/QS3D.ProductBootstrapper/BootstrapperOptions.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/InstallPlanningSmoke.cs`

**Interfaces:**
- Produces: `InstallPlanner.CreatePlan(ProductFamilyManifest, IReadOnlyList<HostInstallation>, BootstrapperOptions)`, `InstallPlan`, `InstallPlanItem`, `PlanDiagnostic`.

- [ ] **Step 1: Write RED tests** proving exact generation matching, pending component diagnostics, no neighboring-generation fallback, default `detected`, explicit product filtering, standalone opt-in, explicit `--host-generation`, and deterministic ordering.
- [ ] **Step 2: Add dry-run orchestration fake assertions:** downloader and installer call counters remain zero.
- [ ] **Step 3: Run smoke RED.**
- [ ] **Step 4: Implement planner.** Explicit requests with zero installable items are usage/plan errors; detected mode may report unavailable hosts without substituting packages.
- [ ] **Step 5: Run smoke GREEN.**
- [ ] **Step 6: Commit** `feat(distribution): plan exact host packages and dry runs #47`.

### Task 4: Bounded package download and integrity verification

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/PackageAcquisition.cs`
- Create: `tools/QS3D.ProductBootstrapper/PackageVerifier.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/PackageAcquisitionSmoke.cs`

**Interfaces:**
- Produces: `IPackageDownloader.DownloadAsync(ProductComponent, string destination, CancellationToken)`, `PackageVerifier.Verify(path, component)`.

- [ ] **Step 1: Write RED fixture tests** for success, expected-length mismatch, >512 MiB declaration rejection, digest mismatch, overwrite refusal and failed-download cleanup.
- [ ] **Step 2: Run smoke RED.**
- [ ] **Step 3: Implement streamed `HttpClient` downloader** into a newly created private temp directory; never construct a shell command; retain only safe URL text in logs.
- [ ] **Step 4: Implement SHA-256 verification** with `CryptographicOperations.FixedTimeEquals` after exact byte-length verification.
- [ ] **Step 5: Run smoke GREEN.**
- [ ] **Step 6: Commit** `feat(distribution): verify exact downloaded package bytes #47`.

### Task 5: Safe EXE and ZIP installation strategies

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/PackageInstallation.cs`
- Create: `tools/QS3D.ProductBootstrapper/SafeZipInstaller.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/PackageInstallationSmoke.cs`

**Interfaces:**
- Produces: `IPackageInstaller.InstallAsync(...)`, `IProcessRunner.RunAsync(exe, args, cancellationToken)`, `SafeZipInstaller.ExtractAndPublish(...)`.

- [ ] **Step 1: Write RED EXE tests** for exact argument-array pass-through and nonzero child exit propagation.
- [ ] **Step 2: Write RED ZIP tests** rejecting absolute paths, drive prefixes, `..`, duplicate normalized paths, >10,000 entries and >512 MiB expanded content; verify no final destination mutation on failure.
- [ ] **Step 3: Run smoke RED.**
- [ ] **Step 4: Implement EXE process runner** with `UseShellExecute=false`, argument list tokens, no shell string, wait-for-exit and exact exit code.
- [ ] **Step 5: Implement ZIP preflight + staging extraction + publish** only to an explicit safe root/destination contract.
- [ ] **Step 6: Run smoke GREEN.**
- [ ] **Step 7: Commit** `feat(distribution): add safe package installation strategies #47`.

### Task 6: Coordinator, deterministic CLI and failure aggregation

**Files:**
- Create: `tools/QS3D.ProductBootstrapper/BootstrapperCoordinator.cs`
- Create: `tools/QS3D.ProductBootstrapper/Program.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/CoordinatorSmoke.cs`
- Create: `tests/QS3D.ProductBootstrapper.SmokeTests/CliSmoke.cs`

**Interfaces:**
- Produces: `BootstrapperCoordinator.RunAsync(...)`, terminal `ComponentInstallResult`, `BootstrapperReport`, process exit codes 0/1/2.

- [ ] **Step 1: Write RED tests** for fail-fast, `--continue-on-error`, cleanup, dry-run, invalid args, explicit no-plan error and deterministic `HOST/PLAN/RESULT/SUMMARY` output.
- [ ] **Step 2: Run smoke RED.**
- [ ] **Step 3: Implement option parser** for `--manifest`, `--product`, `--host-generation`, `--dry-run`, `--continue-on-error`, `--keep-downloads`.
- [ ] **Step 4: Implement coordinator** so integrity failure never reaches installer and later items are marked `SkippedAfterFailure` in default fail-fast mode.
- [ ] **Step 5: Redact query strings and normalize output.**
- [ ] **Step 6: Run smoke GREEN.**
- [ ] **Step 7: Commit** `feat(distribution): add family bootstrapper CLI orchestration #47`.

### Task 7: Truthful initial family manifest and documentation

**Files:**
- Create: `installer/product-family.manifest.json`
- Create: `docs/PRODUCT-FAMILY-INSTALLER.md`
- Create: `scripts/check-product-family-bootstrapper.py`
- Modify: `scripts/check-standalone-source-boundary.py`
- Test: `tests/QS3D.ProductBootstrapper.SmokeTests/ManifestContractSmoke.cs`

**Interfaces:**
- Manifest consumed by packaging/CLI; guard consumed by authoritative validation.

- [ ] **Step 1: Write RED source guard** requiring bootstrapper isolation, no vendor managed assembly references, exact manifest schema tokens and pending flags for unavailable durable packages.
- [ ] **Step 2: Run `python scripts/check-product-family-bootstrapper.py` and verify RED.**
- [ ] **Step 3: Add truthful initial manifest:** integrated AutoCAD = disabled/pending until #85 durable release; BricsCAD V26 = disabled/pending; BricsCAD V25 auto-install disabled until archive destination contract is explicit; standalone may be pinned only if exact release evidence is used.
- [ ] **Step 4: Add docs** covering compatibility, pending status, CLI, trust model and native evidence boundary.
- [ ] **Step 5: Extend standalone source-boundary guard** to reject references from standalone runtime projects to `QS3D.ProductBootstrapper` or Autodesk/Bricsys assemblies.
- [ ] **Step 6: Run guards GREEN.**
- [ ] **Step 7: Commit** `docs(distribution): lock product-family bootstrapper boundaries #47`.

### Task 8: Authoritative validation and Windows packaging

**Files:**
- Create: `scripts/package-family-bootstrapper.ps1`
- Modify: `scripts/validate.ps1`
- Modify: `.github/workflows/ci.yml`
- Optionally modify: `QS3D.CAD.sln` only if required for developer discoverability; validation must still call project paths explicitly.

**Interfaces:**
- Produces: `artifacts/family-bootstrapper/QS3D-Family-Setup-win-x64.exe`, manifest and SHA-256 sidecars.

- [ ] **Step 1: Add bootstrapper build/smoke to `scripts/validate.ps1` after existing standalone validation.**
- [ ] **Step 2: Implement package script** using `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`, copy local manifest, and emit SHA-256 sidecars.
- [ ] **Step 3: Add CI packaging smoke** asserting executable/manifest/sidecars exist and hashes match; retain artifact only on non-PR runs.
- [ ] **Step 4: Run full `./scripts/validate.ps1`.** Expected: existing Platform/host/desktop gates plus bootstrapper smoke all PASS.
- [ ] **Step 5: Run `./scripts/package-windows.ps1 -SkipValidation`** and verify existing standalone installer smoke still PASS.
- [ ] **Step 6: Run `./scripts/package-family-bootstrapper.ps1`** and verify family bootstrapper package/hash PASS.
- [ ] **Step 7: Commit** `ci(distribution): validate and package QS3D family bootstrapper #47`.

### Task 9: Final exact-head verification and PR handoff

**Files:**
- Update: `docs/agent-work-claims/2026-09-07-chatgpt-gpt56sol-product-family-bootstrapper.md` on the task branch only, marking source handoff state while preserving native/main boundaries.

- [ ] **Step 1: Compare task branch against refreshed `main`; resolve only real drift without force-push.**
- [ ] **Step 2: Run fresh full exact-head CI and require SUCCESS.**
- [ ] **Step 3: Audit changed-file list for accidental overlap with open PRs #21/#23/#25/#27/#29/#31/#33/#35/#37/#39/#41/#43/#45.**
- [ ] **Step 4: Confirm no vendor DLL/binary additions and no runtime reference from standalone projects into bootstrapper.**
- [ ] **Step 5: Open final PR to `main` with exact SHA, CI run, package artifact identity and explicit native/main-write boundary.**
- [ ] **Step 6: Do not merge implementation to `main` without separate explicit owner authorization.**
