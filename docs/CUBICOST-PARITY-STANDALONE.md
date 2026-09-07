# Cubicost-style parity — standalone QS3D-CAD

Updated: 2026-08-15 (UTC+7)  
Tracking: #34

## Architecture

The standalone application consumes the canonical vendor-neutral contracts from `QS3D-Platform`; it does not copy BricsCAD or AutoCAD SDK behavior. This lane advances the exact Platform gitlink to `e029d4ba0de6ffe80575f7aed96affa1db1b9b33`, the CI-green shared Cubicost parity head from Platform #13 / PR #15.

The host project references `QS3D.Platform.Parity` directly. All new commands are reference-host/read-only operations: they may change the application selection set, but they do not append/update/erase drawing entities or mutate semantic project state.

## Commands

- `QSMEPRECOGNIZE` — classify the current selection through the shared Layer/BlockName recognition profile and report Matched/Ambiguous/Unmatched deterministically.
- `QSMEPTAKEOFF metersPerUnit` — convert exact available reference metrics to SI and aggregate MEP quantity through `MepQuantityService`.
- `QSMEPCLASH clearanceMeters metersPerUnit` — convert selected reference extents to SI and run the shared hard/clearance AABB clash service.
- `QSMEPCLASHLOCATE index clearanceMeters metersPerUnit` — review a deterministic bounded pair and atomically replace the selection only after both Handles are confirmed live.
- `QSMEPISSUES clearanceMeters metersPerUnit` — project current clashes into the shared in-memory `CoordinationIssue` model. This deliberately performs no project, file, server or cloud persistence.

## Units and metric truth

Standalone reference drawings do not expose a universal DWG `INSUNITS`, so quantity/clash commands require an explicit finite positive `metersPerUnit` value. Clearance is supplied directly in meters. Zero, negative, NaN/Infinity or overflow fail closed.

Metrics are sourced in this order:

1. explicit finite non-negative adapter properties `QS3D.Mep.Length`, `QS3D.Mep.Area`, `QS3D.Mep.Volume` and optional integer `QS3D.Mep.Count`;
2. exact known reference primitive properties for Line, Circle and the current rectangle-backed Polyline representation;
3. otherwise zero for the unavailable dimension.

The adapter never treats an extents diagonal as physical curve length and never treats a generic 3D bounding box as physical volume.

Optional text properties `QS3D.Mep.System`, `QS3D.Mep.Specification` and `QS3D.Mep.Region` override safe standalone fallbacks.

## Safety and determinism

Unknown/ambiguous recognition is skipped rather than guessed. Clash results are filtered so at least one participant is recognized as MEP. Pair order follows deterministic shared-service ordering. `QSMEPCLASHLOCATE` reviews at most 200 pairs; stale Handle resolution returns a failure without changing the existing selection.

`QSMEPISSUES` demonstrates shared issue/status/severity/CadReference interoperability only. Persistence, collaboration, assignments across users and cloud synchronization remain service/application scope.

## Validation

Deterministic standalone smoke covers:

- default MEP + structural recognition from layer tokens;
- SI takeoff conversion and explicit unit refusal;
- hard clash detection;
- exactly-two live Handle locate;
- in-memory shared coordination issue projection;
- unchanged drawing database revision and semantic workspace revision across all parity commands.

The source guard verifies the exact Platform pin, command registrations, shared-service usage, explicit unit/metric contracts and absence of drawing-write/proprietary SDK paths.

## Native boundary

Status for native DWG/ODA/AutoCAD/BricsCAD fidelity is **PENDING_NATIVE**. QS3D-CAD CI proves only the standalone/reference implementation and pinned shared Platform behavior. It does not constitute licensed native-CAD qualification.
