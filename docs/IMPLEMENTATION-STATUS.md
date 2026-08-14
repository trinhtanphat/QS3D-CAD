# QS3D CAD implementation status

**Date:** 2026-08-14 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_WINDOWS_INSTALLER_RELEASED / NATIVE_DWG_EVIDENCE_PENDING`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Current cross-repository baseline

- Exact `QS3D-Platform` gitlink used by the validated CAD/release source: `986d5baa00065a1adb53e53733811beb9cf0a2d9`.
- Platform checkpoint `986d5baa00065a1adb53e53733811beb9cf0a2d9` passed QS3D Platform CI #94, run `31778331523`.
- Platform current documentation HEAD `fa6f0705cc0a65ff5de5f382dccb4de53ee21969` passed QS3D Platform CI #95, run `31778887449`.
- CAD validates the exact recursive Platform gitlink, runs Platform source/reference gates and smoke, then standalone host/smoke, Windows desktop build and real Inno Setup installer packaging.

## Implemented standalone/reference foundation

- Headless application host, CLI/WPF shell scaffold and deterministic in-memory CAD adapter.
- Stable drawing IDs/CAD handles, transactions, stale-transaction guard and coordinated CAD+semantic undo/redo journal.
- Drawing commands including `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST` with finite-coordinate overflow guards.
- Transactional layers/current-layer ownership and static block definition/insert/delete/list workflows.
- Semantic workspace plus `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT`, `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`.
- Standalone health reports `ORPHAN_HANDLE` for missing live handles and `CAD_REFERENCE_DRAWING_MISMATCH` for foreign-drawing semantic ownership.
- Semantic project attachment is drawing-affinity guarded; rejected foreign-drawing state does not replace the current document project.
- Bootstrap/package Open closes a newly opened document if semantic attachment fails, preventing half-open semantic state.
- Bootstrap JSON rejects undefined CAD entity and semantic kind enum values; shared Platform CAD contracts independently reject undefined/Unknown entity kinds.
- Shared unit-aware quantity/schedule pipeline through `QSQTY` and `QSSCHEDULE`.
- Deterministic reference viewport/hit-test/snap/polygon-selection plus reference Xref/Layout/Plot services.
- Desktop File Open/Save uses the validated `.qs3d` package/recovery path.

## Persistence and package integrity

The `.qs3d` bootstrap/reference package layer provides exact ZIP entry/manifest contracts, bounded reads, SHA-256 and length validation, exact media types, semantic/drawing identity consistency, canonical semantic snapshot hashing, same-directory temporary publication, validated `.qs3d.bak` backup/recovery and fail-closed behavior when primary and backup are invalid.

This container is real project file I/O, but its drawing payload remains the reference/bootstrap representation. It is **not native DWG fidelity evidence**.

## Windows CI evidence

- CAD exact source `14b0d374769cb571bb5150654ea8f0e209ea658d` passed QS3D CAD CI #17, run `31778681150`, including authoritative validation and real Inno Setup installer/checksum smoke.
- The released source `a3c0e6d098f02c8cfbb594020b20930491339d59` is three commits ahead of `14b0d374...`; the delta changes only release/bootstrap workflow files. Release run #5 independently reran authoritative validation and Windows packaging successfully on exact `a3c0e6d...`.
- CAD release-workflow checkpoint `b4e2baf9f770849da12a552b15d66d516c7c4a06` passed QS3D CAD CI #23, run `31779472462`, including the real-installer gate.
- CAD provenance-cleanup checkpoint `d0fb075efe475b041b001830a8b18fcf86c0ad2e` passed QS3D CAD CI #24, run `31779833493`, including authoritative validation and real Inno Setup installer/checksum smoke.

## Published Windows preview

GitHub prerelease `v0.1.0-preview.2` is published at exact tag/source `a3c0e6d098f02c8cfbb594020b20930491339d59` by Windows Release run #5, `31779290396`.

Published assets:

- `QS3D-CAD-Setup-win-x64.exe` — 50,175,121 bytes — SHA-256 `5f6912569bbc43bbcfb7bdd18c902c35457aa91964c5d3b6f01264174d5c562e`.
- `QS3D-CAD-Setup-win-x64.exe.sha256` — checksum sidecar.

The release workflow completed checkout, .NET/Inno setup, authoritative validation, installer packaging, artifact upload, GitHub Release publication and published-asset verification successfully. The Actions artifact sidecar was independently checked against the installer bytes and matches the same SHA-256 above.

The old `v0.1.0-preview.1` release attempt failed during Inno metadata packaging before publication; preview.2 fixes that issue by keeping numeric PE installer product-version metadata separate from preview SemVer.

`.github/workflows/bootstrap-preview-tag.yml` is now a read-only manual verifier for the already-published preview.2 tag/assets and cannot dispatch a new release. Future releases use `.github/workflows/release-windows.yml` with an explicit exact `source_sha` and refuse cross-SHA replacement of an existing version tag.

The preview installer is self-contained for `win-x64`. Authenticode signing remains pending an approved production signing certificate, so Windows SmartScreen may warn.

## Production backend qualification policy

Production backend selection remains fail-closed: the backend must be `Native`, advertise only known required capabilities, have unambiguous evidence, and match the exact QS3D-CAD source SHA plus native backend version. Reference/in-memory success cannot satisfy production native qualification.

## Native/local-only work still required

The following remain outside source/reference/installer qualification and require a legally licensed native CAD SDK/runtime:

- native DWG/DXF open/save/save-as and edited/no-op round-trip fidelity;
- production GPU viewport/device lifecycle and large-drawing behavior;
- native hit testing, grips and true intersection/tangent/perpendicular OSNAP;
- native Xrefs, layouts/page setup and PDF/printing;
- production-kernel TRIM/EXTEND/OFFSET/FILLET/CHAMFER/JOIN/BREAK;
- native 3D primitives/extrude/revolve/sweep/loft and B-Rep booleans;
- text/fonts/hatches/dimensions/tables/images and dynamic/proxy object fidelity;
- final `.qs3d` native drawing payload/reference integration;
- Authenticode signing, clean-machine native-runtime evidence and large-project performance.

No `DWG_PASS`, `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` claim is made by this checkpoint.
