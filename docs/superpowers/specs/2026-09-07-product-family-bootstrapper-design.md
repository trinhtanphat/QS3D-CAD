# QS3D Product-Family Bootstrapper Design

**Status:** approved architecture captured for implementation  
**Issue:** #47  
**Claim:** `docs/agent-work-claims/2026-09-07-chatgpt-gpt56sol-product-family-bootstrapper.md`  
**Baseline:** `main@b3bd44202c9b5a771379735ecb00d7527f96010f`

## 1. Purpose

Build a Windows x64 distribution-only bootstrapper that gives the QS3D product family one installation entry point while preserving separate native host implementations.

The bootstrapper may detect installed supported AutoCAD and BricsCAD generations, choose an exact pinned QS3D component, verify downloaded bytes, and run the component's declared installation strategy. It must not turn standalone `QS3D-CAD` into an AutoCAD/BricsCAD-dependent application.

## 2. Product boundary

`QS3D-CAD` remains the standalone CAD product. The bootstrapper is a separate executable under `tools/QS3D.ProductBootstrapper`; it is not referenced by `QS3D.Cad.Host`, `QS3D.Cad.Desktop`, `QS3D.Cad.Cli`, or any public standalone runtime project.

Host ownership remains:

- `QS3D-BricsCAD`: BricsCAD-native payloads and native qualification;
- `QS3D-AutoCAD`: AutoCAD-native payloads and native qualification;
- `QS3D-CAD`: standalone product plus distribution bootstrapper tooling;
- `QS3D-Platform`: host-neutral domain/contracts.

No AutoCAD/BricsCAD managed SDK assemblies or plugin binaries are committed into this repository.

## 3. Supported host model

The planner recognizes these logical host generations:

- AutoCAD 2021, 2022, 2023, 2024;
- AutoCAD 2025, 2026, 2027;
- BricsCAD V25, V26;
- standalone QS3D-CAD as an optional product component independent of host detection.

Host discovery and package availability are separate. Detecting a supported CAD host does not mean the manifest has a qualified package for it.

A detected host with no enabled exact package is reported as `PackageUnavailable` and is never mapped to a neighboring version or stale asset.

## 4. Architecture

```text
Program / CLI
  -> BootstrapperOptions
  -> IHostDiscovery
       -> WindowsRegistryHostDiscovery (production)
       -> FakeHostDiscovery (tests)
  -> ProductFamilyManifestLoader + ManifestValidator
  -> InstallPlanner
  -> PackageCoordinator
       -> IPackageDownloader
       -> PackageVerifier
       -> IPackageInstaller
            -> ExeInstaller
            -> ArchiveInstaller (only with explicit destination contract)
  -> InstallReport / exit code
```

All orchestration logic depends on small interfaces so tests run without registry writes, network calls, child installers, licensed CAD, or administrator privileges.

## 5. Manifest contract

A schema-1 JSON manifest is shipped beside the bootstrapper distribution and validated before any download or process launch.

Root fields:

- `schemaVersion`: exactly `1`;
- `familyVersion`: nonblank SemVer-like identity;
- `generatedFromSource`: exact 40-hex source SHA for the bootstrapper repository;
- `components`: bounded array, maximum 32.

Each component contains:

- `id`: unique canonical ID, maximum 80 characters;
- `product`: `autocad`, `bricscad`, or `standalone`;
- `hostGeneration`: exact logical generation such as `2024`, `V25`, or `standalone`;
- `enabled`: boolean;
- `qualification`: `qualified`, `preview`, or `pending`;
- `repository`: exact `owner/repo` allowlisted to the QS3D family;
- `releaseTag`: exact nonblank tag when enabled;
- `sourceSha`: exact 40-hex component source SHA when enabled;
- `assetName`: exact release asset name when enabled;
- `downloadUrl`: absolute HTTPS GitHub release URL when enabled;
- `sha256`: exact 64-hex digest when enabled;
- `bytes`: positive expected length with a 512 MiB hard maximum when enabled;
- `packageKind`: `exe` or `zip` when enabled;
- `installStrategy`: `execute` or `extract` when enabled;
- `arguments`: bounded string array for `execute`;
- `destination`: optional explicit destination contract for `extract`.

Disabled/pending components may omit release/download fields but must still have a unique ID, product, generation and explicit `enabled=false`.

Unknown properties, duplicate logical properties, unsupported enum values, duplicate IDs, duplicate `(product, hostGeneration)` pairs, invalid URL origins, unsafe archive destinations, traversal segments, control characters, oversized strings and inconsistent package/strategy pairs fail validation.

## 6. Trust model

The initial implementation does not fetch a mutable remote manifest. The bootstrapper consumes a local manifest packaged with the bootstrapper; `--manifest <path>` allows an explicit owner/test override.

This keeps manifest trust inside the distributed bootstrapper artifact. A future signed remote update channel is a separate feature and must not be implied here.

Enabled package URLs must be HTTPS and must resolve to the GitHub release-download form for one of these repositories only:

- `trinhtanphat/QS3D-AutoCAD`;
- `trinhtanphat/QS3D-BricsCAD`;
- `trinhtanphat/QS3D-CAD`.

Redirects are accepted only when the HTTP client follows from the allowlisted GitHub release URL to an HTTPS final URI; the downloader never accepts embedded credentials or a manifest URL with a fragment.

## 7. Current package availability policy

The checked-in product-family manifest must be truthful at commit time.

- A component is enabled only when a durable exact release asset, exact source SHA, exact byte length and SHA-256 are known.
- AutoCAD source integration PR #85 is not a durable release asset until it is explicitly authorized to land and its release pipeline publishes one; therefore the new 2021-2027 integrated AutoCAD component remains disabled/pending in the initial family manifest.
- BricsCAD V26 remains disabled/pending until a qualified durable V26 release asset and installation contract are available.
- BricsCAD V25 may only be enabled for automatic installation when the archive destination/install contract is explicitly known. A verified ZIP alone is not sufficient authority to guess an install location.
- Standalone QS3D-CAD may be enabled when pinned to an existing exact Setup.exe release asset.

The tests use fixture manifests with enabled EXE/ZIP components so every planner/downloader/installer path is covered independently of current release availability.

## 8. Host discovery

`WindowsRegistryHostDiscovery` is read-only.

AutoCAD discovery enumerates 64-bit and 32-bit registry views under Autodesk AutoCAD roots and maps release keys exactly:

- `R24.0` -> 2021;
- `R24.1` -> 2022;
- `R24.2` -> 2023;
- `R24.3` -> 2024;
- `R25.0` -> 2025;
- `R25.1` -> 2026;
- `R26.0` -> 2027.

BricsCAD discovery enumerates Bricsys BricsCAD roots and accepts only subkeys beginning with canonical `V25` or `V26` generation tokens. Locale/product subkeys may exist below them but do not change the generation.

Duplicate discoveries from registry views/hives are normalized into one deterministic host record per vendor/generation/install-root identity.

An injectable `IRegistryReader` supplies registry values so tests do not touch the real machine registry.

## 9. Selection and planning

Default selection is `detected`: plan only enabled components matching detected host generations. Standalone is not automatically added unless `--product standalone` or `--product all` explicitly requests it.

`--product` accepts exactly:

- `detected` (default);
- `autocad`;
- `bricscad`;
- `standalone`;
- `all`.

For explicit AutoCAD/BricsCAD product selection, detected supported generations are still required unless the caller also supplies `--host-generation <value>` for an explicit advanced/install-image scenario.

The planner returns deterministic `InstallPlanItem` records and separate diagnostics. It never silently substitutes another generation.

## 10. Dry-run

`--dry-run` is a hard no-mutation boundary.

Dry-run may:

- read the manifest;
- read host registry state;
- validate and create the installation plan;
- print exact package identities, URL, SHA-256, source SHA and intended strategy.

Dry-run may not:

- create download/cache directories;
- perform HTTP requests;
- extract archives;
- start processes;
- write registry values;
- modify CAD/plugin directories.

Tests assert zero calls to downloader and installer fakes during dry-run.

## 11. Package acquisition

The default downloader uses `HttpClient` with a bounded timeout and streamed download to a newly created private temporary directory under `%TEMP%`.

Rules:

- maximum component size is 512 MiB;
- enforce expected `bytes` while streaming and again from the completed file;
- refuse overwrite of an existing target path;
- compute SHA-256 from completed bytes;
- compare digest using fixed-time comparison;
- do not execute/extract until size and digest both match;
- clean the private temporary directory in `finally` unless `--keep-downloads` was explicitly supplied for diagnostics;
- do not log tokens, query strings containing secrets, or full local user paths when avoidable.

A failed download or verification produces no installer invocation for that component.

## 12. Installation strategies

### Execute

For `packageKind=exe` + `installStrategy=execute`, start the verified exact executable with manifest-declared arguments using `UseShellExecute=false` and wait for completion. No shell command string is constructed.

Exit code `0` is success. Any other code is a component failure and is retained in the final report.

### Extract

For `packageKind=zip` + `installStrategy=extract`, the manifest must provide an explicit destination contract. The initial implementation supports only destinations rooted below a bootstrapper-defined safe root; absolute arbitrary destinations and `..` traversal are rejected.

ZIP entries are validated before extraction:

- no absolute paths;
- no drive/root prefixes;
- no `..` traversal;
- no empty/duplicate normalized target paths;
- maximum 10,000 entries;
- maximum 512 MiB total uncompressed bytes;
- no reparse/symlink semantics;
- stage into a sibling temporary directory first;
- publish by directory move/replace semantics only after full extraction validation succeeds.

If no explicit safe destination contract exists for a BricsCAD ZIP, that component remains disabled rather than guessing.

## 13. Failure and exit-code semantics

Exit codes:

- `0`: requested plan completed successfully, or dry-run successfully produced a plan;
- `1`: one or more selected component downloads/verifications/installers failed;
- `2`: invalid arguments, invalid manifest, unsupported explicit generation, or no installable plan for an explicit request.

Default real installation is fail-fast after the first component failure. `--continue-on-error` permits later independent components to run but final exit remains `1` when any component failed.

Every attempted component yields one terminal status: `Succeeded`, `DownloadFailed`, `IntegrityFailed`, `InstallFailed`, or `SkippedAfterFailure`.

## 14. CLI output

Output is deterministic plain text suitable for logs:

```text
HOST product=autocad generation=2025 source=registry
PLAN component=autocad-2025-2026 tag=... asset=... sha256=...
RESULT component=autocad-2025-2026 status=Succeeded exitCode=0
SUMMARY planned=1 succeeded=1 failed=0 dryRun=false
```

Secrets and signed query strings are never printed.

## 15. Packaging

The bootstrapper is published as a self-contained Windows x64 single-file executable:

`QS3D-Family-Setup-win-x64.exe`

The distribution ZIP/artifact contains exactly:

- `QS3D-Family-Setup-win-x64.exe`;
- `product-family.manifest.json`;
- `product-family.manifest.json.sha256`;
- `QS3D-Family-Setup-win-x64.exe.sha256`.

It does not embed or redistribute AutoCAD/BricsCAD SDK/plugin payloads.

## 16. Tests and CI

Create a dedicated deterministic console smoke project `tests/QS3D.ProductBootstrapper.SmokeTests` targeting .NET 8.

Required regression coverage:

- manifest valid round trip;
- reject unknown/duplicate/missing/oversized/unsafe manifest fields;
- exact host-generation mapping and duplicate normalization;
- planner exact-match behavior and pending/unavailable diagnostics;
- no neighboring-generation fallback;
- dry-run zero downloader/installer calls;
- successful local fixture acquisition through fake downloader;
- byte-length mismatch;
- SHA-256 mismatch;
- EXE child exit-code propagation through fake process runner;
- continue-on-error aggregation;
- ZIP traversal/duplicate/entry-count/expanded-size rejection;
- staged ZIP publication success against a temporary safe root;
- cleanup after failure;
- source-boundary guard proving existing standalone runtime projects do not reference the bootstrapper or vendor host SDKs.

`./scripts/validate.ps1` adds bootstrapper build + smoke after existing standalone validation, so existing product gates remain authoritative.

CI additionally runs a packaging smoke and retains the bootstrapper artifact on non-PR runs.

Hosted CI does not claim licensed/native AutoCAD or BricsCAD install/runtime PASS.

## 17. Documentation and release handoff

Add `docs/PRODUCT-FAMILY-INSTALLER.md` describing supported generations, pending components, CLI, trust boundaries and native qualification limits.

A family manifest update is release-relevant distribution work and must be reviewed like code because it changes executable package selection.

When AutoCAD #85 later lands and a durable exact release is published, enabling that AutoCAD component is a small follow-up manifest-only lane with exact asset/hash/source evidence. The same applies to BricsCAD V26.

## 18. Non-goals

This work does not:

- merge AutoCAD PR #85 or any BricsCAD PR;
- publish a new host release;
- prove native host compatibility;
- add a remote auto-update service;
- create a new GitHub repository;
- combine AutoCAD and BricsCAD code into one DLL;
- alter standalone CAD runtime behavior;
- bypass signing, licensing, release provenance, or native acceptance gates.
