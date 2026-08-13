# QS3D CAD implementation status

**Date:** 2026-08-13 (UTC+7)  
**Product:** standalone Windows CAD/BIM/QS  
**No BricsCAD runtime is required by this repository's product architecture.**

## Implemented standalone foundation

- Separate `QS3D-CAD` repository/product boundary.
- Exact `QS3D-Platform` Git submodule pin for shared contracts.
- Headless standalone application host and command registry.
- Windows .NET 8/WPF desktop shell scaffold.
- In-memory deterministic database adapter for architecture/tests only.
- Stable CAD handles and document identity.
- Transaction commit/rollback and optimistic stale-transaction guard.
- Application-level undo/redo journal coordinating CAD and semantic changes.
- Fail-closed stale undo/redo when a changed domain mutates outside the command journal.
- Basic bootstrap commands: `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST`.
- Transactional layer operations and current-layer/entity ownership.
- Semantic workspace per drawing.
- `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT` using shared Platform domain/diagnostics/quantity logic.
- Bootstrap persistence with backward-readable schema lineage; current established baseline includes CAD entities, semantic project state and layer/current-layer state.
- Native SDK configuration/readiness probe that can report only `NotConfigured`, `DirectoryMissing` or `ConfiguredUnqualified` before real capability evidence exists.
- Native SDK legal/runtime integration checklist.
- Standalone boundary preflight and Windows end-to-end validation script.

## Static block work

`QS3D-Platform` contains deterministic static block definition/reference contracts and conformance tests. Standalone integration is treated separately from real DWG block fidelity: production support still requires native round-trip qualification for nested, attributed, dynamic, anonymous and proxy/custom block behavior.

No bootstrap block implementation may be used as evidence of DWG dynamic-block compatibility.

## Current desktop limitation

The WPF shell is an application shell, **not yet a production CAD viewport**. The bootstrap UI visualizes drawing/database state without claiming native GPU rendering.

Production viewport work is blocked behind the licensed native adapter and must prove:

- real model-space rendering;
- pan/zoom;
- hit testing;
- selection/highlight;
- incremental invalidation;
- DPI handling;
- large-drawing behavior.

## Current persistence limitation

`*.qs3d-bootstrap.json` is a deterministic architecture/test fixture. It is not the final `.qs3d` product container and is not DWG interoperability evidence.

The production `.qs3d` design still needs a deliberate container/manifest/integrity/atomic-save/migration contract after the native drawing payload/reference strategy is fixed.

## Native SDK blocker

The repository intentionally does not ship or impersonate ODA/other proprietary SDK binaries. A configured SDK directory is still `ConfiguredUnqualified` until the adapter is bound and executable evidence exists.

See `docs/NATIVE-SDK-INTEGRATION-CHECKLIST.md` for the exact native milestones.

## Not yet production-qualified

- native DWG/DXF open/save;
- no-op and edited DWG round-trip fidelity;
- native GPU viewport;
- real OSNAP/grips based on entity geometry;
- TRIM/EXTEND/OFFSET/FILLET/CHAMFER/JOIN/BREAK against a real geometry engine;
- Xrefs;
- paper-space layouts/plot/PDF;
- native 3D primitives/extrude/revolve/sweep/loft;
- B-Rep boolean operations;
- DWG dynamic/proxy object preservation;
- production `.qs3d` container;
- plugin SDK runtime loading;
- installer/signing/clean-machine release evidence;
- large-project production performance.

## Validation status

Deterministic smoke/preflight sources are present. GitHub Actions capacity for the account was observed blocked before runner allocation, so a red run caused by exhausted included Actions minutes is neither a code failure nor a PASS. Until runner capacity is available, exact build/runtime claims require local validation with `scripts/validate.ps1` or another real runner.

## Product completion rule

The standalone product can be described as a BricsCAD replacement only for the capability subset that has passed native file-format/runtime qualification. Architecture scaffolding, in-memory commands or source completeness alone are insufficient.

The first commercially meaningful native milestone is defined in `docs/NATIVE-SDK-INTEGRATION-CHECKLIST.md`: own-process DWG open, real viewport, basic entity/layer/block mapping, transactional native edit+undo, and declared round-trip fidelity without requiring BricsCAD on the customer machine.