# Native CAD SDK integration checklist

**Status:** implementation gate for production DWG/render/3D work  
**Product:** `trinhtanphat/QS3D-CAD` only  
**Shared contracts:** `trinhtanphat/QS3D-Platform`  
**Rule:** no native capability is production-qualified merely because a bootstrap interface, environment probe or in-memory implementation exists.

## 1. Purpose

The standalone product needs a real drawing database, DWG/DXF interoperability, viewport/rendering and eventually a native 3D modeling kernel. Those capabilities must be provided through a legally licensed SDK/runtime adapter rather than by copying BricsCAD binaries or pretending the deterministic in-memory host is a DWG engine.

The initial commercial candidate is ODA-based integration, subject to the owner's executed license and the exact SDK package actually supplied. This repository intentionally does not guess proprietary assembly/file names that have not been verified from that licensed package.

## 2. Hard boundaries

- No `BrxMgd.dll`, `TD_Mgd.dll`, BricsCAD/Teigha host binaries or copied proprietary source in `QS3D-CAD`.
- No native SDK binary is committed unless its redistribution terms explicitly permit that exact artifact and packaging form.
- `QS3D-Platform` remains vendor-neutral. ODA/native types stop at the adapter boundary.
- Runtime pointers/native object IDs never become persisted cross-session QS3D identity.
- Stable Platform `DrawingId` + `CadHandle` remains the cross-session reference form unless a later documented migration replaces it.
- A native adapter must advertise only capabilities that have executable qualification evidence.
- In-memory/bootstrap JSON evidence is never cited as DWG compatibility evidence.

## 3. Local SDK configuration

Current bootstrap discovery uses:

```text
QS3D_ODA_SDK_DIR=<licensed SDK root>
```

The adapter bootstrap may report only these states until real bindings exist:

1. `NotConfigured` — environment variable absent.
2. `DirectoryMissing` — configured path does not exist.
3. `ConfiguredUnqualified` — directory exists, but no product capability is yet proven.
4. Later adapter-specific states may be added only after exact SDK contents/API bindings are verified.

Do not treat `ConfiguredUnqualified` as a successful native runtime qualification.

## 4. First native vertical slice

Implement in this order after the licensed SDK is available:

### NATIVE-001 — SDK load boundary

- create isolated adapter project under `src/QS3D.Cad.Native.*`;
- resolve SDK from explicit local/build configuration;
- fail closed when required files/runtime are absent or version-incompatible;
- keep vendor types internal to the adapter;
- emit diagnostic version/build information without leaking secrets/license material.

Exit gate: adapter process starts and reports the exact SDK/runtime version on a licensed development machine.

### NATIVE-002 — read-only DWG database

Map real drawing data into Platform snapshots for:

- drawing identity/fingerprint;
- entities and stable handles;
- layers/current layer;
- static block definitions/references;
- extents and basic geometry facts.

Exit gate: a synthetic repository-owned DWG corpus can be opened repeatedly with deterministic normalized results.

### NATIVE-003 — no-op DWG round trip

```text
open input.dwg
→ save output.dwg without semantic edit
→ reopen output.dwg
→ compare supported object graph and metadata
```

Must cover, at minimum:

- layers;
- line/polyline/arc/circle;
- text/MTEXT where supported;
- static/nested blocks;
- dimensions/hatches once claimed;
- layouts once claimed;
- xrefs once claimed;
- dictionaries/XData/proxy preservation policy;
- large coordinates and malformed input handling.

Exit gate: unsupported data is either preserved according to the selected SDK contract or the product blocks save with an explicit fidelity warning. Silent destructive save is not acceptable.

### NATIVE-004 — transactional writes

Map Platform operations to native database transactions:

- append/update/erase entity;
- layer CRUD/current layer;
- block definition/reference;
- commit/rollback;
- stable handle readback;
- undo integration or a product-owned command journal with proven native rollback semantics.

Exit gate: deterministic adapter conformance tests pass against real files and crash/failure injection does not publish partial drawing state.

### NATIVE-005 — viewport

Implement a real viewport adapter with:

- model-space display;
- pan/zoom/zoom extents;
- hit testing/selection;
- highlight;
- incremental invalidation;
- DPI-aware Windows embedding;
- large-drawing progressive rendering/LOD where the chosen SDK supports it.

Exit gate: the WPF entity-list placeholder is no longer the primary drawing canvas for production mode.

## 5. Precision editing gates

Do not advertise general CAD editing until these are implemented against native geometry:

- absolute/relative coordinate input;
- ORTHO;
- polar tracking;
- endpoint/midpoint/center/intersection/perpendicular/tangent/nearest/quadrant object snaps as applicable;
- grips;
- selection window/crossing/fence/lasso semantics;
- MOVE/COPY/ROTATE/SCALE/MIRROR;
- TRIM/EXTEND/OFFSET/FILLET/CHAMFER/JOIN/BREAK;
- UCS and unit conversion rules.

Every snap/edit result must be finite, tolerance-governed and undoable.

## 6. Block fidelity gates

The deterministic bootstrap block implementation is deliberately limited. Production DWG qualification must separately prove:

- static blocks;
- nested blocks;
- transformed inserts;
- attributes;
- annotative behavior if claimed;
- dynamic blocks if claimed;
- proxy/custom entities preservation;
- anonymous blocks and dependency graphs;
- purge/delete safety.

Never infer dynamic-block fidelity from bootstrap `BLOCK`/`INSERT` tests.

## 7. Xref gates

Required before production Xref claims:

- attach/overlay;
- unload/reload;
- relative and absolute paths;
- missing-reference diagnostics;
- nested/circular dependency protection;
- bind policy;
- file-change refresh;
- save/reopen preservation.

Xref dependency identity must not rely on transient native object pointers.

## 8. Layout/plot gates

Required production surface:

- model space;
- paper-space layouts;
- viewport creation/scale;
- page setup;
- paper size/orientation;
- printer/PDF target;
- lineweight/plot-style policy;
- deterministic PDF output tests where licensing permits test automation.

Plot/PDF output is derived deliverable data, never project source of truth.

## 9. 3D modeling gates

Do not implement fake `Solid3d` equivalents in Platform. The native adapter/kernel owns:

- box/cylinder/cone/sphere primitives;
- extrusion/revolve/sweep/loft;
- boolean union/subtract/intersect;
- sectioning;
- topology/tolerance behavior;
- tessellation for display;
- native B-Rep lifetime.

Platform owns semantic geometry intent/results that do not expose vendor topology pointers.

Exit gate for each operation: deterministic synthetic corpus + finite/extreme-coordinate safety + rollback + save/reopen + visual/native validation.

## 10. BIM/QS integration after native drawing works

The standalone product should then bind existing Platform semantics to real native objects:

```text
native drawing entity / geometry facts
        ↓ adapter normalization
Platform semantic project
        ↓ deterministic rules
Platform quantity / diagnostics / dependency planning
        ↓ adapter execution
native generated geometry + QS3D project persistence
```

Priority semantic objects:

- Wall;
- Slab;
- Beam;
- Column;
- Door/Window/Opening;
- Room;
- Curtain Wall;
- Foundation;
- Rebar;
- Finish.

Generated geometry remains derived from semantic intent and can be regenerated.

## 11. `.qs3d` production container gate

The existing `*.qs3d-bootstrap.json` is a deterministic architecture fixture only.

Production `.qs3d` design must specify:

- manifest/schema version;
- drawing payload/reference strategy;
- semantic project state;
- families/rules;
- quantity caches as rebuildable derived data;
- view/layout metadata;
- checksums/integrity;
- bounded reads;
- atomic publication;
- backup/recovery;
- migration policy;
- optional encryption/signing policy if business requirements demand it.

No destructive conversion of existing `.qsdb` projects is permitted without an explicit migration/export path.

## 12. Qualification corpus

Use only repository-owned synthetic fixtures in public source.

Maintain categories:

- `DWG-OPEN-*`
- `DWG-ROUNDTRIP-*`
- `ENTITY-*`
- `LAYER-*`
- `BLOCK-*`
- `XREF-*`
- `LAYOUT-*`
- `PLOT-*`
- `GEO2D-*`
- `GEO3D-*`
- `VIEW-*`
- `PERF-*`
- `MALFORMED-*`

Customer/private DWGs may be used only in an appropriately private qualification lane and must never be committed to this public repository.

## 13. Performance gates

Measure separately rather than using a single vague “fast” claim:

- open time;
- first frame;
- pan/zoom frame time;
- selection latency;
- command latency;
- save time;
- memory;
- 100k / 500k / 1M entity scenarios where representative;
- large block/xref trees;
- BIM/QS regeneration and schedule calculation.

Performance regressions need exact fixture + machine/runtime context.

## 14. Release evidence

A native-capable release record must identify:

- `QS3D-CAD` source SHA;
- pinned `QS3D-Platform` SHA/version;
- exact native SDK/runtime version;
- target OS/architecture;
- qualification corpus version;
- passed/failed capabilities;
- installer/signing result;
- known fidelity exceptions.

No evidence from `QS3D-BricsCAD` may be relabeled as standalone evidence.

## 15. Definition of native milestone complete

The first commercially meaningful native milestone is complete only when all are true:

1. licensed SDK is actually available to the build/runtime lane;
2. standalone opens a repository-owned DWG through its own process;
3. real GPU/native viewport displays it;
4. layer/entity/basic block data map through Platform contracts;
5. at least one edit is committed and undone through the native adapter;
6. no-op and edited DWG round trips pass the declared fidelity corpus;
7. save failures are fail-closed/atomic at the product boundary;
8. exact-SHA clean-machine qualification exists;
9. no customer needs BricsCAD installed to run this standalone milestone.

Until then, the in-memory host is valuable architecture/test infrastructure, not a replacement claim for BricsCAD.