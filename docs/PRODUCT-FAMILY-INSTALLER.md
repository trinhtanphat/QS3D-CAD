# QS3D Product-Family Bootstrapper

The product-family bootstrapper is distribution tooling for choosing the correct QS3D package for installed CAD hosts. It does not turn standalone `QS3D-CAD` into an AutoCAD or BricsCAD plugin and does not copy vendor SDK assemblies into this repository.

## Supported host generations

Host discovery recognizes these exact generations:

- AutoCAD 2021 (`R24.0`), 2022 (`R24.1`), 2023 (`R24.2`), 2024 (`R24.3`), 2025 (`R25.0`), 2026 (`R25.1`) and 2027 (`R26.0`).
- BricsCAD V25 and V26.
- Standalone QS3D-CAD is an explicit optional selection and is not inferred from a vendor host.

Recognition is not the same thing as release qualification. A host is installable only when `installer/product-family.manifest.json` contains an exact enabled component for that product/generation.

## Current release truth

The initial family manifest intentionally fails closed:

- AutoCAD 2021-2027 are `disabled` until the integrated AutoCAD carrier has a durable exact release asset. AutoCAD 2026 additionally retains an explicit native runtime-matrix qualification boundary.
- BricsCAD V25 preview `v0.1.0-preview.10316` is pinned by repository, release tag, exact source SHA, asset bytes and SHA-256, but remains disabled because the ZIP installation destination contract is not yet part of the published release contract.
- BricsCAD V26 remains disabled until a durable qualified public release asset is available.
- Standalone preview 4 is pinned as historical reference evidence but remains disabled because it is not a current native-DWG-qualified product-family payload.

The bootstrapper must never substitute an adjacent host generation, scrape an arbitrary `latest` asset, or silently enable a pending component.

## Manifest trust model

Schema 1 is a local packaged manifest. Unknown or duplicate case-insensitive properties are rejected. Enabled packages require exact repository, tag, source SHA, asset name, byte count, SHA-256, package kind, install strategy and a clean HTTPS `github.com/<repo>/releases/download/<tag>/<asset>` URL.

Downloads are streamed with a 512 MiB bound, redirects are rejected, declared length and SHA-256 are verified before installation, and failed downloads are removed. EXE packages execute with `UseShellExecute=false` and tokenized arguments. ZIP packages are preflighted for traversal, absolute/drive paths, duplicates, entry count and expanded-size limits, then extracted into staging and atomically published only to an explicit destination that does not already exist.

## CLI

```powershell
QS3D.ProductBootstrapper.exe --manifest product-family.manifest.json --dry-run
QS3D.ProductBootstrapper.exe --product autocad --host-generation 2026 --dry-run
QS3D.ProductBootstrapper.exe --product bricscad --host-generation V25 --dry-run
QS3D.ProductBootstrapper.exe --standalone --dry-run
```

Options: `--manifest`, `--product`, `--host-generation`, `--standalone`, `--dry-run`, `--continue-on-error`, `--keep-downloads`.

Dry-run performs host discovery and planning only. It does not download, execute, extract or stage packages. Explicit selections with no enabled exact component exit with usage/plan status 2 rather than falling back.

## Evidence boundary

Hosted CI proves source/build/smoke/package integrity for the bootstrapper only. It does not prove licensed AutoCAD/BricsCAD runtime behavior, native DWG fidelity, plugin UI behavior, or a vendor host release. Those remain owned and qualified in `QS3D-AutoCAD` and `QS3D-BricsCAD` respectively.
