# QS3D CAD — Standalone Master Planning

**Status:** architecture baseline  
**Product form:** standalone Windows x64 CAD/BIM/QS desktop application  
**Primary dependency:** `QS3D-Platform` for host-neutral domain/application contracts  
**Compatibility goal:** production-grade DWG-centric AEC CAD with native QS3D BIM/QS workflows; not a literal 100% clone of every BricsCAD product module.

## 1. Product mission

Build `QS3D-CAD` as a standalone CAD/BIM/QS product that can be installed and used without BricsCAD. The user should be able to open/edit/save production drawings, perform precise 2D/3D CAD work, author semantic building elements, calculate quantities, create schedules/BQ, and use QS3D-specific construction workflows in one application.

The strategic target is not “copy every BricsCAD feature.” The target is:

1. enough general CAD fidelity to replace the host for AEC/QS workflows;
2. excellent DWG interoperability;
3. native BIM/QS objects rather than metadata-only add-ons;
4. stronger quantity, rebar, schedule, model-health and automation workflows than a generic CAD host + plugin combination.

## 2. Repository/product boundary

This repository owns:

- `QS3D.exe` desktop shell;
- desktop UI and workspace layout;
- standalone document manager;
- command line/command router;
- native CAD-host adapter implementing `QS3D-Platform` interfaces;
- drawing/kernel SDK integration behind adapter boundaries;
- viewport/render adapter;
- selection, grips, snapping and editor interaction;
- application undo/redo integration;
- standalone file/open/save lifecycle;
- installer/update/signing packaging;
- standalone performance and DWG qualification suites;
- product-level plugins/extensions SDK.

This repository must not:

- contain BricsCAD managed assemblies;
- require `NETLOAD`;
- use BricsCAD as its viewport/database/editor;
- copy proprietary vendor source/binaries;
- expose third-party SDK types through the public QS3D application API;
- duplicate host-neutral domain/quantity/persistence logic that belongs in `QS3D-Platform`.

## 3. Target architecture

```text
QS3D.exe
 |
 +-- Desktop Shell
 |    Ribbon / menus / command line / palettes / properties
 |
 +-- Application Host
 |    documents / commands / undo / settings / workspaces
 |
 +-- QS3D-Platform
 |    domain / BIM / QS / persistence / diagnostics / CAD contracts
 |
 +-- Standalone CAD Adapter
 |    drawing DB / transactions / entities / layers / blocks / xrefs
 |
 +-- Geometry/Drawings SDK Adapter
 |    DWG/DXF / native geometry / database
 |
 +-- Viewport Adapter
      GPU rendering / selection / highlighting / LOD
```

All third-party SDK usage stays in dedicated integration projects. The shell and product features depend on QS3D abstractions, not vendor classes.

## 4. SDK strategy

### Preferred bootstrap direction

Use a legally licensed engineering SDK for DWG database and rendering rather than implementing the DWG binary format and full geometry kernel from scratch. ODA Drawings/Visualize are candidates, but the source tree must keep the vendor adapter optional and isolated until commercial/license terms are finalized.

### Required adapter projects

```text
QS3D.Cad.Host
QS3D.Cad.Desktop
QS3D.Cad.Native.Abstractions
QS3D.Cad.Native.InMemory
QS3D.Cad.Native.Oda        (optional build; SDK supplied externally)
QS3D.Cad.Rendering.Abstractions
QS3D.Cad.Rendering.Oda    (optional build)
```

No proprietary SDK binaries are committed.

## 5. Bootstrap without proprietary SDK

The repository must remain buildable/testable on a normal development machine before the native SDK is available. Therefore P0 includes an in-memory drawing/runtime adapter implementing the same contracts used by the future native adapter.

This enables deterministic tests for:

- document lifecycle;
- entity CRUD;
- transactions;
- undo/redo;
- command routing;
- selection identity;
- layer and block semantics;
- core drawing operations that can be represented independently.

The in-memory adapter is not a production DWG engine and must never be marketed as one.

## 6. Desktop technology

Initial target:

- Windows x64;
- .NET 8+;
- WPF for first production desktop shell to maximize reuse of existing QS3D UI expertise/source patterns;
- native SDK interop isolated from UI;
- per-monitor DPI aware;
- crash-safe document recovery;
- async background work only for non-mutating/controlled operations.

Cross-platform support is deferred until the Windows product and SDK choices are stable.

## 7. CAD document model

The standalone host must own:

- application/document manager;
- active document and multi-document lifecycle;
- drawing database;
- persistent entity handles;
- spaces/layouts;
- symbol tables/styles;
- transaction scopes;
- undo/redo journal;
- dirty/save state;
- recovery/autosave;
- xref dependency graph;
- events with drawing ownership.

A command can only mutate the document inside a write/transaction scope. Failed/cancelled commands leave no partial mutation.

## 8. 2D entity baseline

P1/P2 entity coverage:

- Line;
- Polyline/LwPolyline;
- Arc;
- Circle;
- Ellipse;
- Spline;
- Point;
- Hatch;
- Text/MText;
- Block definition/reference;
- Dimensions;
- Leader/MLeader;
- Table;
- Raster image reference.

Each entity requires:

- create/read/update/delete;
- bounds/extents;
- transform;
- selection/highlight;
- properties;
- save/reopen round trip in the production adapter;
- undo/redo coverage.

## 9. Core 2D command baseline

Creation:

- LINE;
- PLINE;
- CIRCLE;
- ARC;
- RECTANG;
- POLYGON;
- TEXT/MTEXT;
- HATCH.

Modification:

- MOVE;
- COPY;
- ROTATE;
- SCALE;
- MIRROR;
- OFFSET;
- TRIM;
- EXTEND;
- FILLET;
- CHAMFER;
- STRETCH;
- BREAK;
- JOIN;
- EXPLODE;
- ERASE.

View/navigation:

- PAN;
- ZOOM;
- ZOOM EXTENTS/WINDOW;
- REGEN;
- 3DORBIT when 3D viewport arrives.

## 10. Precision/editor features

Production CAD requires precision interaction, not only geometry storage.

Required:

- absolute coordinates;
- relative coordinates;
- polar coordinates;
- dynamic distance/angle entry;
- ORTHO;
- POLAR tracking;
- grid/snap;
- object snap;
- object snap tracking;
- UCS/WCS;
- selection cycling;
- crossing/window/fence/lasso selection;
- grips and grip editing;
- command cancellation/repeat;
- keyboard aliases.

Object snaps:

- endpoint;
- midpoint;
- center;
- quadrant;
- intersection;
- apparent/projected intersection if supported;
- perpendicular;
- tangent;
- nearest;
- extension;
- insertion.

## 11. Layers, styles and blocks

Required production surfaces:

- layer create/rename/delete/freeze/lock/on-off/color/linetype/lineweight;
- linetype management;
- text styles;
- dimension styles;
- block definitions;
- nested blocks;
- attributes;
- block editing workflow;
- dynamic/parametric block compatibility according to native SDK capability;
- purge/audit-oriented health checks.

## 12. DWG/DXF interoperability

A file is not considered supported because it merely opens.

Qualification path:

```text
open -> inspect -> edit -> save -> reopen -> compare -> open in independent CAD
```

Corpus categories:

- versions of DWG supported by chosen SDK;
- layers/styles;
- nested blocks and attributes;
- dimensions/annotative data;
- hatch;
- layouts/paper space;
- xrefs;
- images;
- SHX/TTF font fallback;
- dictionaries/XData-like custom metadata;
- large coordinates;
- proxy/custom objects;
- malformed/corrupt inputs;
- very large entity counts.

No release may silently strip unsupported data. Unsupported/proxy content must be preserved when the SDK allows it and surfaced diagnostically.

## 13. Layout, plot and publishing

Required before professional AEC release:

- model space;
- paper space/layouts;
- paper viewports;
- viewport scales;
- page setup;
- printer/PDF publishing;
- lineweights;
- plot-style mapping;
- title-block workflow;
- multi-sheet publish;
- deterministic printable extents.

## 14. Xrefs

Required:

- attach;
- overlay;
- unload/reload;
- detach;
- relative/absolute path policy;
- missing-reference diagnostics;
- circular-reference prevention;
- bind when supported;
- xref layer visibility controls;
- dependency graph and reload invalidation.

## 15. 3D CAD baseline

Primitives:

- box;
- cylinder;
- sphere;
- cone;
- wedge where supported.

Modeling:

- extrude;
- revolve;
- sweep;
- loft;
- press/pull style workflow where supported;
- union;
- subtract;
- intersect;
- section/section planes;
- 3D transforms and UCS.

Robustness requirements:

- explicit tolerance policy;
- deterministic failure reporting;
- no partial boolean mutation;
- topology/reference invalidation documented;
- stress corpus for degenerate and extreme inputs.

## 16. Rendering/viewport

Viewport architecture must support:

- hardware acceleration;
- scene graph separate from domain model;
- spatial index/BVH/R-tree equivalent;
- view frustum culling;
- incremental invalidation;
- LOD/progressive display for large drawings;
- selection/highlight overlays;
- visual styles;
- 2D/3D navigation;
- section/clipping;
- DPI-safe overlays and transient graphics.

Performance rule: editing one entity must not rebuild the entire drawing scene.

## 17. Native BIM/QS objects

QS3D semantic objects become first-class product concepts:

- Wall;
- Slab;
- Beam;
- Column;
- Door;
- Window/Opening;
- Room/Space;
- Curtain Wall;
- Foundation;
- Rebar;
- Finish/Material.

Architecture:

```text
semantic object
  -> authoritative parameters/relationships
  -> deterministic generator
  -> native CAD geometry
  -> quantities/schedules/health
```

Generated CAD geometry is derived and traceable to semantic ownership. Direct manual CAD edits to generated geometry must have an explicit reconcile/detach policy.

## 18. Project Browser and BIM workspace

Target tree:

```text
Project
 +- Buildings
 |   +- Floors
 |       +- Zones
 |       +- Walls
 |       +- Columns
 |       +- Beams
 |       +- Slabs
 |       +- Rooms
 +- Families
 +- Materials
 +- Schedules
 +- Sheets
 +- Quantity/BQ
 +- Health
```

Selection is bidirectional: browser -> viewport and viewport -> semantic element.

## 19. QS/quantity features

The standalone product must retain and extend QS3D value:

- semantic takeoff;
- quick takeoff from CAD entities;
- plan-to-3D recognition/review;
- quantity rules;
- BQ review;
- material/finish schedules;
- opening/door schedules;
- rebar/BBS schedules;
- CSV/XLSX export;
- traceability from reported quantity to semantic/source CAD objects;
- recalculation and dirty/freshness indicators.

## 20. Rebar and structural workflows

Progressively implement:

- column rebar;
- beam longitudinal/stirrups;
- slab rebar;
- structural wall rebar;
- foundation rebar;
- shape/code catalog abstraction;
- bar marks;
- schedules/BBS;
- generated geometry ownership;
- regional/standard-specific catalogs as data packages rather than hard-coded assumptions.

## 21. File/project strategy

### DWG

DWG remains first-class interchange and production drawing format.

### `.qs3d`

Standalone project package may contain:

```text
manifest
semantic model
families/materials
quantity state
views/workspace metadata
source/provenance
optional linked/embedded drawings
recovery/migration metadata
```

The format must be versioned, validated, bounded and migratable. It must not intentionally lock users out of exporting/retaining usable DWG deliverables.

## 22. Plugin/automation ecosystem

Expose a QS3D-owned public extension API after internal contracts stabilize.

Initial .NET extension model:

```text
IQs3dPlugin
IQs3dApplication
IDocumentManager
ICadDocument
ICadDatabase
ICadEditor
ICommandRegistry
IUiExtensionRegistry
```

Command registration supports deterministic undoable commands.

Later scripting layers:

1. public .NET SDK;
2. command/script DSL;
3. Python automation;
4. optional LISP-compatible subset only if customer demand justifies it.

Do not make full AutoLISP/ARX compatibility a P0 requirement.

## 23. AI architecture

AI may propose actions but must not directly mutate drawing memory.

```text
prompt
 -> planner
 -> typed QS3D command plan
 -> validation/capability/security checks
 -> preview/confirmation when destructive
 -> normal transaction/undo command execution
```

All AI-created geometry remains attributable and undoable. Deterministic CAD/domain code is the final authority.

## 24. Reliability

Required application services:

- autosave;
- crash recovery;
- journal/logging with privacy-safe redaction;
- atomic project publication;
- drawing recovery path;
- cancellation of long operations;
- watchdog/progress for heavy imports/regeneration;
- memory pressure handling;
- safe close with unsaved changes;
- clean shutdown of native SDK resources.

## 25. Performance targets

Performance budgets are scenario based, not marketing-only.

Baseline benchmark tiers:

- 100k entities;
- 500k entities;
- 1M entities;
- multi-xref AEC drawing;
- large BIM semantic project;
- quantity recalculation;
- large rebar schedule.

Track:

- open time;
- first interactive frame;
- pan/zoom responsiveness;
- selection latency;
- command mutation latency;
- save time;
- peak memory;
- incremental regen cost.

## 26. Security and licensing

- no embedded vendor secret/license bypass;
- SDK licenses obtained and documented before commercial distribution;
- third-party notices generated for releases;
- installers signed when production credentials exist;
- update manifests signed/verified;
- update channels separated by stable/beta/dev;
- bounded downloads and atomic rollback;
- untrusted drawing/project input parsed defensively;
- extension/plugin loading policy configurable and auditable.

## 27. Product editions

Potential packaging after capability exists:

### Viewer/Free

- open/view supported drawings;
- layers;
- measure;
- markup;
- export/print subset.

### Pro

- full supported 2D editing;
- blocks/xrefs/layouts/plot;
- production drawing workflows;
- basic 3D.

### BIM/QS

- semantic BIM;
- plan-to-3D;
- quantity/BQ;
- schedules;
- rebar;
- model health;
- advanced automation/AI.

Edition enforcement must be product licensing logic, never drawing corruption or intentional format lock-in.

## 28. Release qualification

### Source/unit gates

- build warnings treated as errors in product code;
- unit tests;
- platform adapter contract tests;
- persistence fuzz/property tests;
- dependency/license scan.

### Native adapter gates

- exact SDK version recorded;
- DWG round-trip corpus;
- geometry command corpus;
- save/reopen tests;
- memory/resource leak tests;
- malformed input tests.

### Desktop gates

- clean machine installation;
- DPI/scaling;
- multi-document lifecycle;
- crash recovery;
- file associations;
- update/rollback;
- large drawing performance.

A source-only pass is not native-runtime qualification.

## 29. Roadmap

### Phase 0 — foundation

- planning/architecture docs;
- .NET solution;
- desktop shell boundary;
- command registry;
- document service contracts;
- in-memory standalone adapter;
- tests;
- no proprietary SDK required.

### Phase 1 — native SDK spike

- legal/commercial SDK selection;
- isolated native adapter;
- open/save one synthetic DWG;
- render one drawing;
- selection and entity identity proof;
- transaction/undo proof.

### Phase 2 — usable 2D alpha

- primary 2D entities;
- create/modify commands;
- layers;
- precision input;
- OSNAP;
- grips;
- properties;
- DWG round trip.

### Phase 3 — production drafting beta

- blocks/attributes;
- dimensions;
- hatch;
- layouts/plot/PDF;
- xrefs;
- text/font handling;
- audit/recovery;
- large drawing performance.

### Phase 4 — 3D

- solid primitives;
- extrude/revolve/sweep/loft;
- booleans;
- sections;
- 3D navigation/visual styles.

### Phase 5 — native QS3D BIM/QS

- semantic element authoring;
- project browser;
- floor/zone/family workflows;
- quantity/BQ;
- schedules;
- plan-to-3D;
- model health.

### Phase 6 — advanced construction

- curtain walls;
- rooms/finishes;
- rebar/BBS;
- advanced recognition;
- collaboration/package improvements.

### Phase 7 — ecosystem

- public SDK;
- scripting;
- AI command planner;
- extension marketplace policy;
- enterprise deployment/licensing.

## 30. Definition of a credible 1.0

`QS3D-CAD 1.0` is not declared until all of the following are true:

- standalone executable runs without BricsCAD;
- chosen production drawing SDK is legally distributable under the product terms;
- supported DWG corpus round-trips without silent destructive loss;
- core 2D editing, precision, layers, blocks, layouts, plot and xrefs are production-usable;
- undo/redo and crash recovery are trustworthy;
- large AEC drawings meet documented responsiveness budgets;
- the same released `QS3D-Platform` semantic/quantity contracts are shared with the BricsCAD product;
- at least the primary Wall/Beam/Column/Slab/Room/Openings QS workflows work natively;
- quantity/BQ outputs retain source traceability;
- installer/update/security/runtime qualification is completed on exact release artifacts.

## 31. Immediate implementation backlog

1. scaffold repository/solution/build rules;
2. add host/native/render abstractions;
3. implement in-memory drawing host;
4. implement command registry and transactional command execution;
5. implement document/new/open/save abstraction for synthetic JSON-backed bootstrap files;
6. implement initial Line/Polyline/Circle entities and transformations in the in-memory adapter;
7. implement selection set and basic editor state;
8. implement undo/redo transaction journal;
9. add tests for commit/rollback/undo/identity;
10. add native SDK integration placeholder that fails clearly until an external SDK path/license is configured;
11. add migration/integration documentation for `QS3D-Platform` and `QS3D-BricsCAD`;
12. only then begin production DWG adapter work.

This document is the master execution plan. Feature work may refine implementation details, but standalone ownership, vendor isolation, DWG-fidelity requirements, deterministic transactions/undo, and shared `QS3D-Platform` ownership are architectural invariants.