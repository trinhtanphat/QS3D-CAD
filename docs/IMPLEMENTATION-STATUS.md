# QS3D CAD implementation status

**Date:** 2026-08-14 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_WINDOWS_INSTALLER_RELEASED / NATIVE_DWG_EVIDENCE_PENDING`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Current development baseline

- Latest validated CAD development checkpoint: `088257236d1055a7686011b9bc2d33a26c275116`.
- Exact `QS3D-Platform` gitlink in that checkpoint: `6f720e796334a4a1acc93e0fe8736ab938412913`.
- Platform checkpoint `6f720e796334a4a1acc93e0fe8736ab938412913` passed QS3D Platform CI #100, run `31783236730`.
- CAD checkpoint `088257236d1055a7686011b9bc2d33a26c275116` passed QS3D CAD CI #39, run `31788345638`, including authoritative validation and real Inno Setup installer/checksum smoke.
- Current published preview source remains `c33698efb80b259ed2ec02f1f79142256a35e8c9` (`v0.1.0-preview.3`); the newer development drafting/UI commits do not move or rewrite that release tag.
- CAD validates the exact recursive Platform gitlink, runs Platform source/reference gates and smoke, then standalone host/smoke, Windows desktop build and real Inno Setup installer packaging.

The temporary one-shot preview.3 publisher was added only to publish the exact validated `c33698ef...` candidate because the connected automation surface could not dispatch the standard manual release workflow directly. After successful publication it was removed from `main`; the release tag remains bound to `c33698ef...` and the standard hardened release workflow remains the permanent release surface.

## Implemented standalone/reference foundation

- Headless application host, CLI and WPF desktop workspace over the deterministic in-memory CAD adapter.
- Interactive reference 2D viewport with grid/axes, entity rendering, click selection, Line/Rectangle/Circle point picking, Erase/Move/Copy workflows, layer/current-layer controls, properties, command/messages panes, keyboard shortcuts, zoom-to-extents and sample drawing action.
- Entity list supports Ctrl/Shift extended selection; the editor selection, viewport highlights and multi-object Move/Copy preparation stay synchronized for direct GUI workflows.
- QS3D branding/logo integrated into the repository and desktop workspace; the desktop executable and Inno installer use the QS3D application icon.
- Stable drawing IDs/CAD handles, transactions, stale-transaction guard and coordinated CAD+semantic undo/redo journal.
- Drawing commands include `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `COPY`, `SELECT`, `ERASE`, `LIST` with finite-coordinate overflow guards.
- `MOVE` and `COPY` support multiple distinct source handles transactionally. Missing sources and coordinate overflow fail without partial commits; copies preserve source layers and select the newly created entities.
- Block-reference translation keeps extents and `QS3D.InsertionX` / `QS3D.InsertionY` metadata aligned for both Move and Copy.
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
- The preview.2 release source `a3c0e6d098f02c8cfbb594020b20930491339d59` independently reran authoritative validation and Windows packaging successfully during Windows Release run #5, `31779290396`.
- Interactive workspace checkpoint `238da96994361ff3a6fd54cd5755ddb1174726be` passed QS3D CAD CI #27, run `31782994405`.
- Platform-repin checkpoint `d36b29a7e45b13e7867fffdbe631452a720a191b` passed QS3D CAD CI #28, run `31783347077`.
- Application/installer icon checkpoint `afe62ffdedad5a4f517dfbd856b49166271d5005` passed QS3D CAD CI #30, run `31783852979`.
- Preview.3 source checkpoint `c33698efb80b259ed2ec02f1f79142256a35e8c9` passed QS3D CAD CI #31, run `31784331886`, including authoritative validation and the real-installer gate.
- Transactional Copy/multi-Move checkpoint `e4a8af3e88a01bc2976d334c54407df542493cc6` passed QS3D CAD CI #37, run `31787751169`.
- Desktop Move/Copy exposure checkpoint `596343ebb3e86a64cbd26525cde00bff8a20d9d2` passed QS3D CAD CI #38, run `31788034576`.
- Extended-selection development checkpoint `088257236d1055a7686011b9bc2d33a26c275116` passed QS3D CAD CI #39, run `31788345638`, including authoritative validation and the real-installer gate.

## Published Windows previews

### Current preview: `v0.1.0-preview.3`

GitHub prerelease `v0.1.0-preview.3` is published at exact tag/source `c33698efb80b259ed2ec02f1f79142256a35e8c9` by publication run `31784716993`. That run completed exact checkout, release-identity verification, .NET/Inno setup, authoritative validation and packaging, release creation, then tag/asset/checksum verification successfully.

Published assets:

- `QS3D-CAD-Setup-win-x64.exe` — 50,151,198 bytes — SHA-256 `b68e2e8de2a142794eede3f69a38a183808a8aefd8a6eea9a421150ade1263d8`.
- `QS3D-CAD-Setup-win-x64.exe.sha256` — checksum sidecar.

GitHub release metadata reports the same installer SHA-256 above, and the publication workflow verified the generated installer bytes against the generated sidecar before completing.

The current development checkpoint is newer than preview.3. The published preview.3 binaries therefore do not claim to contain the post-release transactional Copy/multi-Move and extended-selection UI changes until a future version is intentionally released from an exact validated source SHA.

### Prior immutable preview: `v0.1.0-preview.2`

The prior GitHub prerelease `v0.1.0-preview.2` remains at exact tag/source `a3c0e6d098f02c8cfbb594020b20930491339d59`; it was not moved or replaced while publishing preview.3.

Its installer remains 50,175,121 bytes with SHA-256 `5f6912569bbc43bbcfb7bdd18c902c35457aa91964c5d3b6f01264174d5c562e` plus the matching checksum sidecar.

`.github/workflows/bootstrap-preview-tag.yml` remains a read-only verifier for the already-published preview.2 provenance and cannot dispatch a release. Future regular releases use `.github/workflows/release-windows.yml` with an explicit exact `source_sha` and refuse cross-SHA replacement of an existing version tag.

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
