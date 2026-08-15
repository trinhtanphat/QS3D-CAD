# QS3D CAD document reliability

This document describes the standalone reference application's save-state, autosave and recovery workflow. It is product reliability behavior for the current `.qs3d` reference package and does not claim native DWG crash recovery.

## Save-state model

Each open drawing has an application-owned logical state cursor in addition to database and semantic revisions.

- A new/opened document starts at a clean checkpoint.
- A successful application-journal mutation advances to a unique state ID.
- A successful manual project save records the current state ID as the saved checkpoint and records the primary `.qs3d` path.
- Undo restores the state ID that existed before the command.
- Redo restores the command's post-state ID.
- Therefore undo can return exactly to a saved checkpoint and clear the dirty indicator.
- Database or semantic changes observed outside the application command journal create a divergent logical state and remain dirty. Existing stale-history checks still fail closed rather than undoing across an unknown external mutation.

A successful `.qs3d` open/save establishes the known clean checkpoint. If primary loading fails and the validated `.bak` is used, the requested primary path remains the future Save target; the backup path never becomes the default primary path.

## Autosave

`StandaloneAutosaveService` writes normal validated QS3D packages named:

```text
<DrawingId>.autosave.qs3d
```

Desktop autosaves are stored below the current user's local application data directory:

```text
QS3D/CAD/Autosave
```

Rules:

- only dirty documents are autosaved;
- autosave uses the same bounded package writer and validation format as normal `.qs3d` packages;
- autosave never marks a document clean;
- discovery is bounded to 256 newest matching files;
- each discovered snapshot must pass the full package loader; corrupt snapshots are skipped;
- successful manual save removes the stale autosave for that drawing on a best-effort basis;
- explicit discard on application close also clears the tracked autosave.

## Recovery

At desktop startup, validated autosave snapshots are discovered. If any exist, QS3D offers to open the newest snapshot.

A recovered autosave:

- opens as a normal standalone document;
- remains dirty;
- has no primary project path;
- is visibly marked `[Recovered]` in the window title;
- requires an explicit Save/Save As before it becomes a clean primary project;
- keeps the recovery snapshot until successful publication or explicit discard.

## Desktop save and close behavior

- `Ctrl+S` saves to the known primary path after the first open/save; if no primary path exists, Save As is shown.
- `Ctrl+Shift+S` always uses Save As.
- The title includes `*` while the active drawing is dirty.
- A background dispatcher check updates the dirty caption frequently and writes dirty-only autosaves on a two-minute cadence.
- Application close checks every open document.
- **Yes** saves every dirty drawing; canceling any required Save As or encountering a save error cancels application close.
- **No** explicitly discards unsaved changes and removes tracked autosaves for those drawings.
- **Cancel** leaves the application open unchanged.

## Scope boundary

This workflow strengthens the standalone `.qs3d` reference product. It does not establish native DWG save fidelity, operating-system process crash guarantees, native SDK resource cleanup, or production telemetry qualification. Those remain separate native/release gates in `PLANNING.md`.
