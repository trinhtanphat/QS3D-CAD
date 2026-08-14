# QS3D CAD implementation status

**Date:** 2026-08-14 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_WINDOWS_INSTALLER_RELEASED / NATIVE_DWG_EVIDENCE_PENDING`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Current development baseline

- Validated CAD development checkpoint: `d36b29a7e45b13e7867fffdbe631452a720a191b`.
- Exact `QS3D-Platform` gitlink in that checkpoint: `6f720e796334a4a1acc93e0fe8736ab938412913`.
- Platform checkpoint `6f720e796334a4a1acc93e0fe8736ab938412913` passed QS3D Platform CI #100, run `31783236730`.
- CAD checkpoint `d36b29a7e45b13e7867fffdbe631452a720a191b` passed QS3D CAD CI #28, run `31783347077`, including authoritative validation and real Inno Setup installer/checksum smoke.
- CAD validates the exact recursive Platform gitlink, runs Platform source/reference gates and smoke, then standalone host/smoke, Windows desktop build and real Inno Setup installer packaging.

This development baseline is newer than the already-published preview.2 source. Updating the development gitlink does **not** move or rewrite the published preview.2 tag or assets.

## Implemented standalone/reference foundation

- Headless application host, CLI and WPF desktop workspace over the deterministic in-memory CAD adapter.
- Interactive reference 2D viewport with grid/axes, entity rendering, click selection, Line/Rectangle/Circle point picking, Erase/Move workflows, layer/current-layer controls, properties, command/messages panes, keyboard shortcuts, zoom-to-extents and sample drawing action.
- QS3D branding/logo integrated into the repository and desktop workspace.
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
- Current Platform reference services fail closed on undefined Xref/plot/view/selection enums and unknown snap flag bits; conformance severity, dirty-state flags and parity expectations are also guarded against undefined values.
- Desktop File Open/Save uses the validated `.qs3d` package/recovery path.

## Persistence and package integrity

The `.qs3d` bootstrap/reference package layer provides exact ZIP entry/manifest contracts, bounded reads, SHA-256 and length validation, exact media types, semantic/drawing identity consistency, canonical semantic snapshot hashing, same-directory temporary publication, validated `.qs3d.bak` backup/recovery and fail-closed behavior when primary and backup are both invalid.

This container is real project file I/O, but its drawing payload remains the reference/bootstrap representation. It is **not native DWG fidelity evidence**.

## Windows CI evidence

- CAD exact source `14b0d374769cb571bb5150654ea8f0e209ea658d` passed QS3D CAD CI #17, run `31778681150`, including authoritative validation and real Inno Setup installer/checksum smoke.
- The released source `a3c0e6d098f02c8cfbb594020b20930491339d59` is three commits ahead of `14b0d374...`; that delta changes only release/bootstrap workflow files. Release run #5 independently reran authoritative validation and Windows packaging successfully on exact `a3c0e6d...`.
- CAD provenance-cleanup checkpoint `d0fb075efe475b041b001830a8b18fcf86c0ad2e` passed QS3D CAD CI #24, run `31779833493`.
- Interactive workspace checkpoint `238da96994361ff3a6fd54cd5755ddb1174726be` passed QS3D CAD CI #27, run `31782994405`.
- Latest validated development checkpoint `d36b29a7e45b13e7867fffdbe631452a720a191b` passed QS3D CAD CI #28, run `31783347077`, including the real-installer gate.

## Published Windows preview

GitHub prerelease `v0.1.0-preview.2` remains published at exact tag/source `a3c0e6d098f02c8cfbb594020b20930491339d59` by Windows Release run #5, `31779290396`.

Published assets:

- `QS3D-CAD-Setup-win-x64.exe` — 50,175,121 bytes — SHA-256 `5f6912569bbc43bbcfb7bdd18c902c35457aa91964c5d3b6f01264174d5c562e`.
- `QS3D-CAD-Setup-win-x64.exe.sha256` — checksum sidecar.

The release workflow completed checkout, .NET/Inno setup, authoritative validation, installer packaging, artifact upload, GitHub Release publication and published-asset verification successfully. The Actions artifact sidecar was independently checked against the installer bytes and matches the same SHA-256 above.

The old `v0.1.0-preview.1` release attempt failed during Inno metadata packaging before publication; preview.2 fixes that issue by keeping numeric PE installer product-version metadata separate from preview SemVer.

`.github/workflows/bootstrap-preview-tag.yml` is a read-only manual verifier for the already-published preview.2 tag/assets and cannot dispatch a new release. Future releases use `.github/workflows/release-windows.yml` with an explicit exact `source_sha` and refuse cross-SHA replacement of an existing version tag.

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
