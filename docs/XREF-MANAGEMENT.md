# Standalone Xref Management

This document describes the deterministic external-reference lifecycle exposed by the QS3D-CAD standalone reference adapter.

## Scope

The pinned QS3D-Platform reference service provides an in-memory, document-scoped xref model with these states:

- `Loaded`
- `Unloaded`
- `Missing`
- `Unresolved`
- `CircularDependency`

The current reference adapter implements attach/reload/unload/detach only. It is not a native DWG xref engine.

## Explicit commands

### `XREFLIST`

Lists all external references in deterministic name order.

### `XREFSTATUS name`

Shows one xref's name, kind, current status and path.

### `XREFATTACH name path [Attach|Overlay]`

Adds a document-scoped external reference. The default kind is `Attach`.

The reference adapter checks the supplied path at attach time:

- existing file -> `Loaded`
- missing file -> `Missing`

Duplicate xref names fail without replacing the existing item.

### `XREFRELOAD name`

Refreshes the xref status from the current path.

### `XREFUNLOAD name`

Marks the xref `Unloaded` without detaching it.

### `XREFDETACH name`

Removes the xref from the current document's reference service.

### `XREFHEALTH`

Produces a deterministic health summary containing counts for:

- total
- loaded
- unloaded
- missing
- unresolved
- circular dependency

`Missing`, `Unresolved` and `CircularDependency` count as problematic references in the summary.

### `XREFRELOADALL`

Reloads every xref in deterministic service order and reports the resulting status counts. The batch checks the command cancellation token between references.

## Legacy compatibility

`XREFREF LIST|ATTACH|RELOAD|UNLOAD|DETACH ...` remains registered for compatibility.

Historically this mixed read/mutation wrapper advertised `CommandFlags.ReadOnly` even though four of its five actions mutate reference-service state. The wrapper now advertises only `RequiresDocument`; the explicit commands above expose precise `ReadOnly` versus `ModifiesDrawing` metadata per operation.

## State and undo boundary

Xref state belongs to `InMemoryAdvancedServicesRegistry.For(document).Xrefs`, not to the CAD database transaction model. Therefore xref lifecycle changes do not increment `ICadDatabase.Revision` and are not currently part of the application's drawing/semantic UNDO journal.

This boundary is intentional and documented rather than pretending xref service mutations are native drawing transactions.

## Document isolation

Every `InMemoryCadDocument` receives its own `InMemoryAdvancedServices` instance. Xrefs attached to one open document do not appear in another document. Reactivating a document returns to that document's own xref service state.

## Validation

The dedicated smoke module covers:

- command registration;
- attach kind parsing;
- loaded/missing status detection;
- list/status/health diagnostics;
- unload/reload/reload-all transitions;
- detach;
- duplicate-name and missing-name failures;
- invalid xref kind rejection;
- legacy `XREFREF` compatibility;
- database-revision non-mutation boundary;
- multi-document xref isolation.

## Fidelity boundary

This implementation does **not** claim:

- native DWG xref table persistence;
- `BIND`, `INSERT`-bind or native detach semantics;
- xref path reassignment or relative-path canonicalization;
- circular-reference detection from DWG dependency graphs;
- nested-DWG loading/resolution;
- filesystem watching / auto-reload;
- xref clipping;
- native overlay propagation rules;
- undo/redo of reference-service state;
- licensed ODA/AutoCAD/BricsCAD runtime qualification.

Those require a native backend or an expanded shared Platform contract and must be qualified separately.
