# Selection Set Management

QS3D-CAD exposes deterministic standalone selection-set operations on top of the Platform `ICadSelection` contract.

## Commands

- `SELSTATUS` — report selected handles and classify each as live or stale.
- `SELHANDLES handle [handle ...]` — replace the selection with explicit live handles.
- `SELADD handle [handle ...]` — add explicit live handles to the current selection.
- `SELREMOVE handle [handle ...]` — remove syntactically valid handles from the selection, including stale handles.
- `SELTOGGLE handle [handle ...]` — toggle selected handles off; toggling a handle on requires that it is live in the drawing.
- `SELHEALTH` — report total/live/stale selection counts without mutation.
- `SELPRUNE` — remove stale handles while preserving all currently live selected handles.

All handle tokens use the Platform hexadecimal `CadHandle` contract. Commands that add handles fail atomically when a requested handle is not live. Duplicate requested handles are normalized deterministically.

## Stale handles

The reference `ICadSelection` stores handles independently from database entities. A selected entity can therefore become stale after drawing undo, erase, external mutation, or another operation that removes the entity without rewriting editor selection. `SELHEALTH` exposes this condition and `SELPRUNE` repairs it explicitly.

## Revision and undo boundary

Selection state is editor state, not drawing database state. Selection-only commands do not create database revisions and are not added to the standalone drawing undo/redo journal. Drawing undo can therefore invalidate an existing selection; this is expected and is why stale-handle diagnostics exist.

## Scope boundary

This lane does not claim native AutoCAD/BricsCAD pickfirst/implied-selection semantics, subentity selection, grips, selection cycling, selection filters over native object classes, native window/polygon interaction, command-reactor integration, or licensed runtime parity.
