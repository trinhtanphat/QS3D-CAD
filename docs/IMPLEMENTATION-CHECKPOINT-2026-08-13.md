# QS3D CAD standalone implementation checkpoint — 2026-08-13

This checkpoint supplements `PLANNING.md` and `docs/IMPLEMENTATION-STATUS.md` for the standalone product.

## Current architecture

`QS3D-CAD` is a separate standalone QS3D product. It consumes `QS3D-Platform` through an exact Git submodule pin and owns the desktop/CLI host plus future legally licensed native drawing/render adapters. It does not turn `QS3D-BricsCAD` into an executable.

The current standalone implementation is intentionally split into:

1. a deterministic bootstrap/reference host that can exercise application semantics without proprietary SDK files;
2. isolated future native adapters for DWG/database/render/kernel behavior;
3. WPF/CLI shell surfaces that must not be mistaken for native viewport qualification.

## Standalone source-ready vertical slices

The current source includes:

- multi-document in-memory bootstrap host;
- transactional LINE/CIRCLE/RECTANG/MOVE/SELECT/ERASE/LIST workflows;
- application-level undo/redo journal coordinating drawing and semantic history with stale-revision guards;
- layer table/current-layer commands and layer ownership;
- block definition/INSERT/BLOCKS/BLOCKDELETE workflows;
- bootstrap schema v4 with backward reads for earlier schemas, layers/current-layer, block definitions and semantic project state;
- semantic tagging and shared Platform readiness checks;
- shared semantic snapshot cloning for attach/undo/redo state;
- Floor/Zone authoring, semantic property mutation and Floor/Zone assignment;
- explicit unit-aware `QSQTY` and `QSSCHEDULE` rule execution through `QS3D.Platform.Quantity`;
- command-level reference viewport/hit-test/snap/polygon-selection surfaces backed by deterministic reference services when available;
- conditional compatibility bridge for older Platform pins; the bridge is excluded automatically when the pinned Platform already contains the native reference-service classes.

Representative standalone commands now include:

- drawing: `LINE`, `CIRCLE`, `RECTANG`, `MOVE`, `SELECT`, `ERASE`, `LIST`, `UNDO`, `REDO`;
- layers: `LAYERS`, `LAYER ...`;
- blocks: `BLOCK`, `INSERT`, `BLOCKS`, `BLOCKDELETE`;
- semantic: `QSTAG`, `QSLIST`, `QSHEALTH`, `QSCOUNT`, `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`, `QSQTY`, `QSSCHEDULE`;
- reference navigation/selection: `VIEW`, `ZOOMEXTENTS`, `ZOOMWINDOW`, `HITTEST`, `SNAP`, `SELPOLY`.

## Deterministic smoke source

Smoke modules cover, among other things:

- command parsing and basic geometry creation;
- transaction and journal undo/redo ordering;
- layer/block persistence and schema-v4 reopen;
- shared canonical CAD ownership readiness;
- semantic Floor/Zone/property/location authoring;
- explicit millimetre-to-metre quantity rules and schedule output;
- advanced reference hit/snap/polygon-selection/view-state behavior without marking drawing or semantic state dirty.

## Important evidence boundary

The bootstrap JSON/container and in-memory database are architecture/test vehicles. They are **not** DWG and are not a substitute for a native CAD SDK.

As of this checkpoint there is no exact-SHA compiler/runtime PASS available from this conversation environment:

- GitHub Actions cannot start jobs because the account Actions budget is exhausted/blocked;
- the execution container does not contain a .NET compiler/SDK;
- downloading the official SDK was blocked by DNS/network resolution.

Accordingly, current claims are **source implemented + statically reviewed + smoke source authored**. Do not label the standalone product `BUILD_PASS`, `DWG_PASS` or production-qualified from this evidence.

## Native implementation/qualification still required

A production standalone release still requires a legally licensed native CAD/DWG/render solution and exact-runtime qualification for:

- DWG open/save/save-as fidelity and corruption recovery;
- real display device, pan/zoom/orbit, redraw/invalidation, DPI and large-drawing performance;
- real object selection/hit testing against native geometry;
- true intersection/tangent/perpendicular/object-snap kernel behavior;
- xrefs, layouts/page setup and native plotting/PDF;
- 3D solids/booleans/meshes/topology;
- native text/fonts/hatches/dimensions/tables/images;
- printing/export and customer drawing compatibility;
- installer, signing, crash recovery and atomic `.qs3d` publication.

Reference-service behavior must remain clearly labeled as deterministic conformance behavior until a native adapter proves the same contract on real files and graphics/kernel APIs.
