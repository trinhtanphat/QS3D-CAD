# QS3D CAD implementation status

**Date:** 2026-08-14 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**Evidence state:** `SOURCE_READY / PENDING_BUILD_AND_NATIVE_EVIDENCE`

`QS3D-CAD` is a standalone product and does not require BricsCAD at runtime. `QS3D-Platform` remains the host-neutral shared layer; `QS3D-BricsCAD` remains the hosted compatibility product.

## Implemented standalone/reference foundation

- Exact `QS3D-Platform` Git submodule pin (`cfb334f2b95feb31a6f5f8969b9b1666ffbfc7c6`) and explicit pin-validation script.
- Headless application host, CLI/WPF shell scaffold and in-memory deterministic CAD adapter.
- Public command catalog exposes registration/membership/name discovery but not executable command instances; all execution remains inside the application journal boundary.
- Stable drawing IDs/CAD handles, transactions, stale-transaction guard and coordinated CAD+semantic undo/redo journal, including mutation journaling when a command fails or throws after publishing a change.
- Drawing commands including `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST` with finite-coordinate overflow guards.
- Transactional layers/current-layer ownership and static block definition/insert/delete/list workflows.
- Semantic workspace plus `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT`, `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`.
- Standalone health adds live-handle validation and reports `ORPHAN_HANDLE` for semantic CAD references missing from the active drawing.
- Shared unit-aware quantity/schedule pipeline through `QSQTY` and `QSSCHEDULE`.
- Deterministic reference viewport/hit-test/snap/polygon-selection services exposed as `VIEW`, `ZOOMEXTENTS`, `ZOOMWINDOW`, `HITTEST`, `SNAP`, `SELPOLY`.
- Reference-only Xref/Layout/Plot lifecycle exposed as `XREFREF`, `LAYOUTREF`, `PLOTREF`. `PLOTREF` records a request and explicitly produces no native file.
- Document-scoped Platform reference services use a weak registry so unreferenced documents are not retained solely by reference-service state.
- Application-owned document lifecycle cleans semantic and journal state even when callers use `app.Documents.Close(...)` directly.
- Desktop multi-document list activates the selected drawing and File Open/Save uses the `.qs3d` package/recovery path.
- CLI uses pre-tokenized `ExecuteCommand` for process arguments, preserves Windows paths/empty arguments and avoids replaying historical editor output after every command.

## Persistence now implemented at bootstrap/reference level

Bootstrap drawing persistence is backward-readable and includes CAD entities, semantic project state, layers/current layer and block definitions.

A `.qs3d` bootstrap/reference package layer now exists with:

- exact ZIP entry set and schema/manifest contract;
- bounded package/manifest/payload reads;
- SHA-256 and declared-length validation;
- exact manifest declaration and media-type validation;
- semantic/drawing identity consistency checks;
- canonical semantic snapshot hash validation;
- malformed JSON and invalid reconstructed-state normalization to `InvalidDataException`;
- same-directory temporary publication before primary replacement;
- validated previous-generation `.qs3d.bak` publication;
- explicit recovery reader returning `RecoveredFromBackup=true`, backup source path and primary failure diagnostics;
- recovery from a hash-valid but structurally corrupt drawing payload;
- fail-closed behavior when primary and backup cannot be validated.

This is real bootstrap/reference container I/O, but the drawing payload is still QS3D bootstrap data. It is **not DWG interoperability evidence** and not yet the final native drawing payload strategy.

## Production backend qualification policy

Production backend selection is fail-closed:

- backend must be available and `Native`;
- required capabilities must be non-empty, known pinned capability flags;
- duplicate backend IDs or qualification evidence IDs are rejected as ambiguous;
- exact 40-character QS3D-CAD source SHA must match qualification evidence;
- exact native backend version must match evidence;
- passing evidence must cover at least one known capability and all required capabilities.

The CAD capability whitelist is aligned to the exact pinned Platform contract, including `BooleanSolids` and `Grips`; nonexistent/unknown capability symbols or flag bits are rejected. Unversioned native descriptors cannot satisfy qualified production selection. Qualification evidence has a deterministic JSON codec for local/CI interchange; that JSON is not a signature or trust root.

## Validation/source gates

`scripts/validate.ps1` runs standalone preflight/source-boundary checks, initializes the pinned Platform submodule, verifies the exact checkout, runs Platform preflight/netstandard boundary checks, then builds/runs Platform and standalone deterministic smoke when a .NET SDK is available.

Source gates lock command execution ownership, lifecycle cleanup, tokenizer/path behavior, package integrity/recovery, live-handle health, backend qualification and exact pinned capability names.

The CAD repository currently has no `.github/workflows` directory, so no CAD GitHub Actions build result exists for this checkpoint.

Platform CI run `31760426233` for exact pin `cfb334f2b95feb31a6f5f8969b9b1666ffbfc7c6` did not start a runner: job `94645403253` reports `runner_id=0`, no steps, and an Actions-budget annotation. This is infrastructure/budget blocking, not source build evidence.

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

No `BUILD_PASS`, `DWG_PASS`, `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` claim is made by this source checkpoint. Exact runtime claims require `scripts/validate.ps1` on a real toolchain and, for native capabilities, exact-SHA/version local evidence.

Reference/in-memory success must never be reported as native CAD success.
