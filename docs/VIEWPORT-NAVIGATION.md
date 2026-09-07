# Standalone Viewport Navigation

This document describes the reference viewport controls exposed by `QS3D.Cad.Host` for the standalone in-memory adapter.

## Commands

- `VIEW` — legacy current-view summary.
- `VIEWSTATUS` — deterministic full view-state summary.
- `VIEWSET targetX targetY targetZ dirX dirY dirZ upX upY upZ width height [Orthographic|Perspective]` — replace the reference view after validation.
- `VIEWCENTER x y [z]` — move the view target while preserving orientation, size and projection. Omitting `z` preserves the current target Z.
- `VIEWPAN dx dy [dz]` — translate the target by a world-space delta.
- `VIEWZOOM factor` — scale view width/height by `1 / factor`; factors above 1 zoom in, factors between 0 and 1 zoom out.
- `VIEWRESET` — restore target `(0,0,0)`, direction `(0,0,-1)`, up `(0,1,0)`, `100 x 100`, orthographic projection.
- `VIEWHEALTH` — report projection, aspect ratio and basis-vector health without changing view state.
- `ZOOMWINDOW x1 y1 x2 y2` — legacy reference zoom-window behavior.
- `ZOOMEXTENTS` — legacy reference zoom-to-drawing-extents behavior.

## Command-state contract

`VIEW`, `VIEWSTATUS` and `VIEWHEALTH` are read-only diagnostics. `VIEWSET`, `VIEWCENTER`, `VIEWPAN`, `VIEWZOOM`, `VIEWRESET`, `ZOOMWINDOW` and `ZOOMEXTENTS` change observable viewport state and therefore do not advertise the `ReadOnly` command flag.

Viewport state is not drawing-database state. Navigation commands do not increment `ICadDatabase.Revision`, do not enter the standalone drawing/semantic undo journal, and are isolated per `InMemoryCadDocument`.

## Validation

All numeric command input is parsed using invariant culture and must be finite. View width, height and zoom factor must be positive. `VIEWSET` delegates final direction/up-vector validation to the pinned Platform `ICadViewportService`: direction and up must be non-zero and must not be parallel, and projection must be a defined `CadViewProjection` value.

Failures are fail-closed: invalid navigation input does not change the current view or drawing revision.

## Fidelity boundary

The pinned Platform adapter models a reference viewport only. This lane does not claim native AutoCAD/BricsCAD camera persistence, DCS/UCS conversion, view twists, perspective lens length, clipping planes, visual styles, named-view dictionaries, viewport entities, smooth animated navigation, wheel/pointer gesture parity, GPU rendering state, or licensed CAD runtime fidelity.

Those features require separate adapter/runtime qualification and must not be inferred from the standalone reference commands.
