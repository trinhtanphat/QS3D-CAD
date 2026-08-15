# Reference 2D primitive suite

This document covers the standalone **reference/in-memory** ARC, POINT and regular POLYGON implementation. It expands the deterministic 2D editor baseline without claiming native DWG or production geometry-kernel fidelity.

## Commands

```text
ARC cx cy radius startAngleDeg sweepAngleDeg
POINT x y
POLYGON sides cx cy radius rotationDeg
```

Common aliases are `A`, `PO` and `POL` respectively.

`ARC` uses center/radius/start-angle/signed-sweep semantics. Sweep must be finite, non-zero and strictly less than 360 degrees in magnitude; use `CIRCLE` for a full revolution.

`POLYGON` creates a regular closed polygon with 3–1024 sides. `radius` is the circumradius and `rotationDeg` is the first-vertex angle from +X.

## Coherent geometry contract

The delivered primitives are not storage-only records. A shared geometry/schema helper owns validation and derived geometry so that the same representation is consumed by:

- true entity extents;
- reference WPF rendering;
- MOVE / COPY / SCALE / ROTATE / MIRROR;
- MEASURE;
- reference OSNAP;
- bootstrap and `.qs3d` package persistence.

Regular polygons use `CadEntityKind.Polyline` with the explicit structural discriminator `QS3D.ReferenceShape=RegularPolygon`. Rectangle-backed Polyline entities retain their previous `x1/y1/x2/y2` semantics and are not reinterpreted as regular polygons.

Arc/polygon schema metadata under `QS3D.*`, plus coordinate/radius properties already treated as structural, cannot be mutated through `SETPROP` / `DELPROP`. This prevents manual metadata edits from desynchronizing geometry and extents.

## Reference snaps

Delivered reference snaps include:

- Arc: endpoint, midpoint-on-sweep, center, quadrants that lie on the sweep, nearest point on the arc.
- Point: endpoint/nearest at the exact point location.
- Regular polygon: vertices, edge midpoints, center and nearest point on an edge.

Existing line-line intersection behavior remains unchanged; this lane does not claim arc-line, arc-arc or polygon intersection solving.

## Measurement

`MEASURE` reports:

- Arc radius, start/sweep angles and arc length.
- Point X/Y coordinates.
- Regular polygon sides, circumradius, perimeter and area.

All measurement is read-only and retains the existing all-or-nothing output behavior for multi-entity requests.

## Transform and history behavior

MOVE, COPY and positive uniform SCALE preserve each delivered primitive schema. ROTATE and MIRROR update the defining angles/orientation and regenerate extents from the transformed geometry. Operations remain transactional and participate in the standalone application UNDO/REDO journal.

## Persistence

The bootstrap schema already stores entity kind, bounds, properties and layer generically. Regression coverage verifies both raw bootstrap save/open and the `.qs3d` ZIP package/recovery path preserve the new primitive schemas.

## Fidelity boundary

This is deterministic standalone reference functionality. It is not evidence of native DWG ARC/LWPOLYLINE encoding, proprietary CAD display-list behavior, production-kernel intersections, native object snaps, or licensed runtime qualification. Arbitrary PLINE vertex/bulge editing is intentionally a separate future lane.
