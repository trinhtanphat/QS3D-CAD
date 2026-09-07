# QS3D Product-Family Bootstrapper

The product-family bootstrapper is distribution tooling for choosing the correct QS3D package for installed CAD hosts. It does not turn standalone `QS3D-CAD` into an AutoCAD or BricsCAD plugin and does not copy vendor SDK assemblies into this repository.

## Supported host generations

Host discovery recognizes these exact generations:

- AutoCAD 2021 (`R24.0`), 2022 (`R24.1`), 2023 (`R24.2`), 2024 (`R24.3`), 2025 (`R25.0`), 2026 (`R25.1`) and 2027 (`R26.0`).
- BricsCAD V25 and V26.
- Standalone QS3D-CAD is an explicit optional selection and is not inferred from a vendor host.

Recognition is not the same thing as release qualification. A host is production-installable only when `installer/product-family.manifest.json` contains an exact enabled component for that product/generation.

## Production manifest truth

`installer/product-family.manifest.json` remains fail-closed:

- AutoCAD 2021-2027 source is integrated on `QS3D-AutoCAD@8dd65a7e5061430f76e027467261beb8f18c69a8` and an engineering candidate exists, but every AutoCAD production component remains disabled pending licensed-host qualification and signed durable release publication. AutoCAD 2026 additionally retains the explicit CLR 8/10 native runtime-matrix boundary.
- The production manifest does not embed any `test-v*` engineering release package. Source guards reject that drift.
- BricsCAD V25 preview `v0.1.0-preview.10316` is pinned by repository, release tag, exact source SHA, asset bytes and SHA-256, but remains disabled because exact native acceptance and an explicit safe ZIP installation destination contract are not yet satisfied for family distribution.
- BricsCAD V26 remains disabled until an exact native-qualified durable public release asset exists together with its explicit install contract and verified bytes/SHA-256.
- Standalone preview 4 is pinned as historical reference evidence but remains disabled because it is not a current native-DWG-qualified product-family payload.

The bootstrapper must never substitute an adjacent host generation, scrape an arbitrary `latest` asset, or silently enable a pending production component.

### Manifest provenance semantics

The schema-1 `generatedFromSource` field is intentionally interpreted by manifest role:

- in `product-family.manifest.json`, it identifies the `QS3D-CAD` source state whose family qualification/provenance state the manifest describes;
- in `product-family.engineering.manifest.json`, it identifies the exact AutoCAD engineering candidate source pinned for qualification.

Vendor package identity is always carried independently by each component's `sourceSha`. The production manifest's current provenance points at `QS3D-CAD@0d59e26d8fa1716d782a68fe5779c4740f58b7fe`, the last behavioral source state before this provenance/docs refresh.

## AutoCAD engineering qualification manifest

`installer/product-family.engineering.manifest.json` is a deliberately separate **non-production** manifest for licensed AutoCAD qualification. It pins the exact post-merge engineering release produced by AutoCAD main CI #266:

- release tag: `test-v0.1.0-ci.266`;
- source SHA: `8dd65a7e5061430f76e027467261beb8f18c69a8`;
- asset: `QS3D-AutoCAD-0.0.0-ci-Setup.exe`;
- bytes: `67998359`;
- SHA-256: `9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de`;
- release state: unsigned engineering prerelease, **not native PASS** and **not production qualified**.

The engineering manifest has one exact-generation entry for AutoCAD 2021 through 2027 so a licensed tester can qualify one installed generation without neighboring-version fallback. On a machine where the requested AutoCAD generation is actually installed, an explicit bootstrapper dry-run is valid:

```powershell
QS3D-Family-Setup-win-x64.exe `
  --manifest product-family.engineering.manifest.json `
  --product autocad `
  --host-generation 2026 `
  --dry-run
```

On hosted/no-AutoCAD environments, explicit host selection intentionally returns `NO_INSTALLABLE_COMPONENT`; that is not a manifest defect. The repository handoff script therefore validates the selected generation itself and uses a manifest-wide bootstrapper dry-run so CI stays independent of licensed host installation.

Remove `--dry-run` only on a licensed test machine when installation is intended. Do not use the engineering manifest as the default production manifest and do not interpret successful installation as native/runtime PASS.

## Native qualification handoff

Use the read-only handoff helper to validate one exact AutoCAD generation against CI #266 and print the authoritative `QS3D-AutoCAD` evidence commands:

```powershell
./scripts/invoke-autocad-native-qualification-handoff.ps1 `
  -HostGeneration 2026 `
  -DryRunBootstrapper

./scripts/invoke-autocad-native-qualification-handoff.ps1 `
  -HostGeneration 2021 `
  -AutoCADRepositoryPath C:\src\QS3D-AutoCAD
```

The handoff script:

- accepts only AutoCAD 2021-2027;
- requires the exact CI #266 tag/source/asset/hash/byte contract;
- requires engineering qualification text to remain `not-native-pass`;
- can run a no-network manifest-wide family-bootstrapper dry-run;
- optionally verifies that a local `QS3D-AutoCAD` checkout contains the native evidence scripts;
- prints exact session/runtime/result/validation commands;
- never writes or changes a passing native result itself.

Before native evidence creation, the exact CI #266 ZIP, Setup.exe, `RELEASE-PROVENANCE.json`, and `SHA256SUMS.txt` must be placed in the `QS3D-AutoCAD/artifacts` directory because the authoritative AutoCAD acceptance scripts verify those exact artifacts. Legacy AutoCAD 2021-2024 generations are qualified individually; one R24.x PASS does not qualify its siblings. The formal modern production matrix still requires separate real-host sessions for AutoCAD 2025, 2026, and 2027 against the same candidate.

## BricsCAD production enablement gates

Family recognition of BricsCAD V25/V26 is already implemented, but recognition is not permission to install production payloads.

### V25

The manifest pins `v0.1.0-preview.10316` and its current exact ZIP metadata, but V25 remains disabled until all of these are true for the chosen family release source:

1. exact real-host BricsCAD V25 native acceptance is PASS;
2. a durable public release asset is intentionally designated for family distribution;
3. the ZIP has an explicit safe destination/install contract rather than an inferred BricsCAD install directory;
4. production manifest bytes and SHA-256 match that exact durable asset.

### V26

V26 remains disabled until the analogous native PASS, durable public family-intended release asset, explicit install contract, and exact bytes/SHA-256 exist.

The family guard rejects any future **enabled** BricsCAD ZIP component that lacks explicit destination metadata. This repository does not invent a destination from vendor installation assumptions.

## Manifest trust model

Schema 1 is a local packaged manifest. Unknown or duplicate case-insensitive properties are rejected. Enabled packages require exact repository, tag, source SHA, asset name, byte count, SHA-256, package kind, install strategy and a clean HTTPS `github.com/<repo>/releases/download/<tag>/<asset>` URL.

Downloads are streamed with a 512 MiB bound. Only the trusted GitHub release redirect chain is accepted; unrelated redirect origins are rejected. Declared length and SHA-256 are verified before installation, and failed downloads are removed. EXE packages execute with `UseShellExecute=false` and tokenized arguments. ZIP packages are preflighted for traversal, absolute/drive paths, duplicates, entry count and expanded-size limits, then extracted into staging and atomically published only to an explicit destination that does not already exist.

## Packaging

The self-contained Windows x64 family-bootstrapper artifact contains exactly the family distribution files:

- `QS3D-Family-Setup-win-x64.exe`;
- `product-family.manifest.json`;
- `product-family.engineering.manifest.json`;
- SHA-256 sidecars for all three files.

Packaging validates both manifests and performs no-network dry-runs before reporting PASS.

## Family preview release

Family distribution is published separately from the standalone `QS3D-CAD` release workflow. The dedicated `.github/workflows/release-family-windows.yml` workflow is manual-only and requires:

- `confirm_release=RELEASE`;
- one exact lowercase 40-character `source_sha` that is reachable from current `main`.

The tag is deterministic: `family-v$VERSION`. With current `VERSION=0.1.0-preview.5`, the intended tag is `family-v0.1.0-preview.5`.

Every family preview release contains exactly six assets:

```text
QS3D-Family-Setup-win-x64.exe
QS3D-Family-Setup-win-x64.exe.sha256
product-family.manifest.json
product-family.manifest.json.sha256
product-family.engineering.manifest.json
product-family.engineering.manifest.json.sha256
```

The workflow refuses a same-name tag that points to a different source SHA, verifies all local sidecars before upload, keeps the release a prerelease, checks the exact six-asset set after publication, compares remote sizes and GitHub SHA-256 digests when exposed, and verifies the final tag target equals the requested source SHA.

Publishing a family preview **does not enable** AutoCAD or BricsCAD production entries. It makes the production fail-closed manifest and engineering qualification manifest available as durable distribution artifacts.

## CLI

```powershell
QS3D-Family-Setup-win-x64.exe --manifest product-family.manifest.json --dry-run
QS3D-Family-Setup-win-x64.exe --product autocad --host-generation 2026 --dry-run
QS3D-Family-Setup-win-x64.exe --product bricscad --host-generation V25 --dry-run
QS3D-Family-Setup-win-x64.exe --standalone --dry-run
```

Options: `--manifest`, `--product`, `--host-generation`, `--standalone`, `--dry-run`, `--continue-on-error`, `--keep-downloads`.

Dry-run performs host discovery and planning only. It does not download, execute, extract or stage packages. Explicit selections with no enabled exact component exit with usage/plan status 2 rather than falling back.

## Evidence boundary

Hosted CI proves source/build/smoke/package integrity for the bootstrapper and family release workflow only. It does not prove licensed AutoCAD/BricsCAD runtime behavior, native DWG fidelity, plugin UI behavior, Authenticode production signing, or a vendor host production release. Those remain owned and qualified in `QS3D-AutoCAD` and `QS3D-BricsCAD` respectively.
