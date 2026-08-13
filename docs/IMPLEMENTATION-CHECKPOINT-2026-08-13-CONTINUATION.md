# QS3D CAD continuation checkpoint — 2026-08-13

Status: **SOURCE_READY / PENDING_BUILD_AND_NATIVE_EVIDENCE**.

This checkpoint extends the earlier standalone implementation checkpoint. It does not supersede the product boundary: `QS3D-CAD` remains the standalone product, `QS3D-Platform` remains host-neutral/shared, and `QS3D-BricsCAD` remains the BricsCAD-hosted compatibility product.

## Source completed in this continuation

- Exact Platform gitlink drift was repaired and repeatedly refreshed while shared source evolved.
- `VIEW`, `ZOOMEXTENTS`, `ZOOMWINDOW`, `HITTEST`, `SNAP`, and `SELPOLY` are registered through document-scoped deterministic reference services.
- `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`, `QSQTY`, and `QSSCHEDULE` are registered in the standalone application.
- `XREFREF`, `LAYOUTREF`, and `PLOTREF` provide explicit reference-only lifecycle surfaces. `PLOTREF` records a request and explicitly states that no native file was produced.
- `.qs3d` bootstrap packages retain bounded ZIP reads, exact entry sets, manifest length/digest checks, semantic/drawing identity checks, and atomic primary publication.
- Validated previous-generation `.qs3d.bak` publication is implemented. Corrupt/missing primary recovery is explicit through `Qs3dBootstrapRecoveryReader`, returns `RecoveredFromBackup=true`, preserves primary failure diagnostics, and fails closed when no valid backup exists.
- `StandaloneCadPackageExtensions` exposes backup-aware save and recovery-aware open APIs for future CLI/WPF use.
- Production backend qualification now requires native availability, required capabilities, exact product source SHA, and exact backend version. Unversioned native descriptors cannot satisfy qualified production selection.
- Qualification evidence has a canonical JSON codec for local/CI interchange. The JSON codec is not a signature or trust root.
- Source-boundary gates now require the registered command surfaces, package integrity markers, backup/recovery surfaces, exact-SHA/version backend policy, shared quantity CSV alias, and deterministic regression modules.

## Evidence boundary

No `.NET` compiler/runtime PASS is claimed by this checkpoint. The conversation execution environment still has no usable `dotnet`, `csc`, `mcs`, or `msbuild`, and prior attempts to bootstrap the .NET SDK were blocked by network/DNS. GitHub Actions availability has also been constrained by account budget.

Therefore all new capabilities above are **source implemented + statically reviewed + smoke source authored**, not runtime-qualified.

## Native work that remains local-only

The following still require legally licensed native SDK/runtime and real drawing/device evidence:

- DWG open/save/save-as/round-trip fidelity;
- real GPU display device, pan/zoom/orbit and invalidation;
- native hit-testing and true intersection/tangent/perpendicular snaps;
- native xrefs, layouts/page setup and PDF/printing;
- B-Rep/solid/boolean/topology behavior;
- text/fonts/hatches/dimensions/tables/images fidelity;
- installer/signing/clean-machine startup and crash recovery;
- exact-SHA native qualification evidence on customer-representative files.

Reference service success must never be reported as native CAD success.
