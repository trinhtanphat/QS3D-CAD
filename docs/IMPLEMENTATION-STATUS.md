# QS3D CAD implementation status

**Date:** 2026-08-13 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_READY / PENDING_BUILD_AND_NATIVE_EVIDENCE`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Implemented standalone/reference foundation

- Exact `QS3D-Platform` Git submodule pin and explicit pin-validation script.
- Headless application host, command registry, CLI/WPF shell scaffold and in-memory deterministic CAD adapter.
- Stable drawing IDs/CAD handles, transactions, stale-transaction guard and coordinated CAD+semantic undo/redo journal.
- Drawing commands including `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST`.
- Transactional layers/current-layer ownership and static block definition/insert/delete/list workflows.
- Semantic workspace plus `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT`, `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`.
- Shared unit-aware quantity/schedule pipeline through `QSQTY` and `QSSCHEDULE`; CSV output routes through Platform.
- Deterministic reference viewport/hit-test/snap/polygon-selection services exposed as `VIEW`, `ZOOMEXTENTS`, `ZOOMWINDOW`, `HITTEST`, `SNAP`, `SELPOLY`.
- Reference-only Xref/Layout/Plot lifecycle exposed as `XREFREF`, `LAYOUTREF`, `PLOTREF`. `PLOTREF` records a request and explicitly produces no native file.
- Document-scoped Platform reference services use a weak registry so unreferenced documents are not retained solely by reference-service state.

## Persistence now implemented at bootstrap/reference level

Bootstrap drawing persistence is backward-readable and includes CAD entities, semantic project state, layers/current layer and block definitions.

A `.qs3d` bootstrap package layer now exists with:

- exact ZIP entry set and schema/manifest contract;
- bounded package/manifest/payload reads;
- SHA-256 and declared-length validation;
- semantic/drawing identity consistency checks;
- canonical semantic snapshot hash validation;
- same-directory temporary publication before primary replacement;
- validated previous-generation `.qs3d.bak` publication;
- explicit recovery reader returning `RecoveredFromBackup=true`, backup source path and primary failure diagnostics;
- fail-closed behavior when primary and backup cannot be validated.

This is real bootstrap container I/O, but the drawing payload is still QS3D bootstrap data. It is **not DWG interoperability evidence** and not yet the final native drawing payload strategy.

## Production backend qualification policy

Production backend selection is fail-closed:

- backend must be available and `Native`;
- required capabilities must be present;
- exact 40-character QS3D-CAD source SHA must match qualification evidence;
- exact native backend version must match evidence;
- evidence must be passing and cover all required capabilities.

Unversioned native descriptors cannot satisfy qualified production selection. Qualification evidence also has a deterministic JSON codec for local/CI interchange; that JSON is not a signature or trust root.

## Validation/source gates

`scripts/validate.ps1` runs standalone preflight/source-boundary checks, initializes the pinned Platform submodule, verifies the exact checkout, runs Platform preflight/netstandard boundary checks, then builds/runs Platform and standalone deterministic smoke when a .NET SDK is available.

Source gates require command registrations, package integrity/recovery surfaces, backend qualification contracts and deterministic regression modules.

## Native/local-only work still required

The following are not production-qualified and must remain in the local/native qualification lane described by `docs/LOCAL-NATIVE-QUALIFICATION.md`:

- native DWG/DXF open/save/save-as and no-op/edited round-trip fidelity;
- real GPU viewport/device lifecycle, pan/zoom/orbit, DPI and large-drawing behavior;
- native hit testing and true intersection/tangent/perpendicular OSNAP;
- native Xrefs and reference resolution;
- paper-space layouts/page setup and real PDF/printing output;
- TRIM/EXTEND/OFFSET/FILLET/CHAMFER/JOIN/BREAK against a production geometry kernel;
- native 3D primitives/extrude/revolve/sweep/loft and B-Rep booleans;
- text/fonts/hatches/dimensions/tables/images and dynamic/proxy object fidelity;
- final `.qs3d` native drawing payload/reference integration;
- plugin runtime loading, installer/signing/clean-machine evidence and crash recovery against the native backend;
- large-project production performance.

## Validation status

No `BUILD_PASS`, `DWG_PASS`, `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` claim is made by this source checkpoint. The current conversation execution environment has no usable .NET SDK/compiler and prior GitHub Actions capacity was blocked before useful runner evidence. Exact runtime claims require `scripts/validate.ps1` on a real toolchain and, for native capabilities, exact-SHA/version local evidence.

Reference/in-memory success must never be reported as native CAD success.
