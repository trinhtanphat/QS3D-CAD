# QS3D CAD implementation status

**Date:** 2026-08-14 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_HARDENED / WINDOWS_BUILD_EVIDENCE_BLOCKED_BY_ACTIONS_BUDGET`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Current cross-repository baseline

- Exact `QS3D-Platform` gitlink: `4d3080ec4eae40fb36eb48215c8891be4e459fe5`.
- That Platform SHA is current-head source/reference validated: QS3D Platform CI #84, run `31776738279`, completed successfully.
- CAD validation checks out the exact recursive Platform gitlink, verifies the pin, runs Platform preflight/netstandard/reference/parity/family gates, builds Platform, runs Platform smoke, then builds/runs standalone host/smoke and the Windows desktop shell.

## Implemented standalone/reference foundation

- Headless application host, CLI/WPF shell scaffold and deterministic in-memory CAD adapter.
- Stable drawing IDs/CAD handles, transactions, stale-transaction guard and coordinated CAD+semantic undo/redo journal.
- Drawing commands including `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST` with finite-coordinate overflow guards.
- Transactional layers/current-layer ownership and static block definition/insert/delete/list workflows.
- Semantic workspace plus `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT`, `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`.
- Standalone health reports `ORPHAN_HANDLE` for missing live handles and `CAD_REFERENCE_DRAWING_MISMATCH` when semantic source/generated ownership points at another DWG identity.
- Semantic project attachment is drawing-affinity guarded; rejected foreign-drawing state does not replace the current document project.
- Bootstrap/package Open closes the just-opened document if semantic attachment fails, avoiding a half-open document with mismatched semantic state.
- Shared unit-aware quantity/schedule pipeline through `QSQTY` and `QSSCHEDULE`.
- Deterministic reference viewport/hit-test/snap/polygon-selection plus reference Xref/Layout/Plot services.
- Application-owned document lifecycle cleans semantic and journal state when documents close.
- Desktop File Open/Save uses the validated `.qs3d` package/recovery path.

## Persistence and package integrity

The `.qs3d` bootstrap/reference package layer provides exact ZIP entry/manifest contracts, bounded reads, SHA-256 and length validation, exact media types, semantic/drawing identity consistency, canonical semantic snapshot hashing, same-directory temporary publication, validated `.qs3d.bak` backup/recovery and fail-closed behavior when primary and backup are both invalid.

This container is real project file I/O, but its drawing payload remains the reference/bootstrap representation. It is **not native DWG fidelity evidence**.

## Windows CI and release lane

- `.github/workflows/ci.yml` is a Windows validation lane that runs `scripts/validate.ps1` and a self-contained `win-x64` publish smoke.
- `scripts/package-windows.ps1` is intentionally restricted to `win-x64`, matching the Inno Setup installer architecture and output identity.
- The release workflow runs only from a `v*` tag or explicit manual dispatch. Manual publication requires `confirm_release=RELEASE`.
- Existing version tags must resolve to the exact checked-out SHA before release assets may be replaced. The workflow refuses cross-SHA asset clobbering.
- The release contract is statically guarded by `scripts/check-release-contract.py`, which is part of authoritative validation.
- Installer output includes a SHA-256 sidecar. Authenticode signing remains pending an approved production signing certificate.

## Current Windows build evidence

A CAD Actions job on 2026-08-14 did not start a Windows runner because the private-repository Actions budget was exhausted (`runner_id=0`, no job steps). Therefore no `BUILD_PASS` is claimed for the current CAD SHA from GitHub-hosted Windows CI. This is an infrastructure/budget blocker, not evidence of a source compile failure.

## Production backend qualification policy

Production backend selection remains fail-closed: the backend must be `Native`, advertise only known required capabilities, have unambiguous evidence, and match the exact QS3D-CAD source SHA plus native backend version. Reference/in-memory success cannot satisfy production native qualification.

## Native/local-only work still required

The following remain outside source/reference qualification and require a legally licensed native CAD SDK/runtime:

- native DWG/DXF open/save/save-as and round-trip fidelity;
- production GPU viewport/device lifecycle and large-drawing behavior;
- native hit testing, grips and true intersection/tangent/perpendicular OSNAP;
- native Xrefs, layouts/page setup and PDF/printing;
- production-kernel TRIM/EXTEND/OFFSET/FILLET/CHAMFER/JOIN/BREAK;
- native 3D primitives/extrude/revolve/sweep/loft and B-Rep booleans;
- text/fonts/hatches/dimensions/tables/images and dynamic/proxy object fidelity;
- final `.qs3d` native drawing payload/reference integration;
- installer signing, clean-machine runtime evidence and large-project performance.

No `DWG_PASS`, `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` claim is made by this source checkpoint.
