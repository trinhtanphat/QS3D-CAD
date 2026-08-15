# Reference precision drafting

This document describes the standalone **reference/in-memory** precision point-input layer. It improves desktop drafting usability while the production native CAD adapter is still a separately qualified lane.

## Desktop controls

- `F3` — toggle reference object snap (OSNAP).
- `F8` — toggle ORTHO constraint for the second/subsequent picked point.
- `F9` — toggle grid snap using the current visible grid spacing.

The active tool status shows the current OSNAP / ORTHO / GRID state. When a supported object snap is acquired, the viewport shows a small snap marker and the cursor status reports the snap kind and entity handle.

## Supported reference snaps

The precision resolver currently supports deterministic candidates for:

- endpoint;
- midpoint;
- center;
- quadrant;
- line-line intersection;
- nearest point on supported reference geometry.

Reference geometry is read from the entity's explicit geometry properties (`x1/y1/x2/y2`, `cx/cy/radius`) rather than guessing geometry from a bounding box. If those properties are absent or invalid, the resolver fails closed for that entity instead of inventing an exact snap.

The current rectangle-backed `Polyline` representation uses its stored two-corner rectangle properties for corner, edge-midpoint and nearest-edge candidates. This does not claim arbitrary polyline vertex fidelity.

`Perpendicular`, `Tangent` and `Extension` flags exist in the shared contract but are intentionally rejected by this standalone precision resolver until their reference geometry semantics are implemented and tested. Native implementations require their own runtime qualification.

## Resolution precedence

Point resolution follows this deterministic order:

1. If OSNAP is enabled and a supported candidate lies within the aperture, the closest candidate wins. Stable tie-breaking uses snap priority, entity handle and coordinates.
2. If no object snap wins and grid snap is enabled, the raw point is quantized to the current visible grid spacing.
3. If no object snap wins and ORTHO is enabled with an anchor point, the resolved point is constrained horizontally or vertically to that anchor.

An exact object snap therefore **wins over grid and ORTHO**. Grid/ORTHO never displace a successfully acquired entity snap.

## Safety and performance

- Precision resolution opens only read-only drawing transactions.
- It must not change drawing revision, undo/redo history or editor selection.
- Non-finite inputs, invalid grid spacing, unsupported snap flags and derived numeric overflow fail explicitly.
- Intersection checks are localized to line entities whose extents are within the current snap aperture before pairwise intersection evaluation.
- Line/circle/rectangle geometry calculations use finite guards and scale-normalized calculations where large coordinate ranges could overflow naive arithmetic.

## Fidelity boundary

This feature is a standalone editor/reference capability. It is **not** evidence of native DWG fidelity, production renderer behavior, production-kernel OSNAP behavior, tangent/perpendicular solving, or licensed native runtime qualification.
