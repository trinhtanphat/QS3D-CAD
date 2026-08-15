# QS3D CAD block management

This document describes the host-neutral block definition and block-reference management workflow in the standalone QS3D CAD reference product.

The implementation uses the pinned `ICadTransaction` block contracts. It does not claim native DWG dynamic-block, attribute, annotative-block or proprietary CAD editor fidelity.

## Existing commands retained

### `BLOCK name handle...`

Creates a block definition from existing drawing entities. For backward compatibility, the base point remains the minimum X/Y/Z of the selected source extents.

### `INSERT name x y [scale] [rotationDegrees]`

Creates a block reference on the current layer. Scale must be finite and greater than zero. Successful insertion now selects the newly inserted reference.

### `BLOCKS`

Lists block definitions.

### `BLOCKDELETE name`

Deletes an unused definition. The standalone host now rejects deletion not only when a live top-level block reference exists, but also when another block definition contains a nested reference to the target definition.

## New commands

### `BLOCKBASE name baseX baseY handle...`

Creates a block definition with an explicit 2D base point while retaining the minimum member Z for the current host-neutral 2D model.

All requested handles are resolved before creation. Missing entities fail the command without publishing a drawing revision.

### `BLOCKINFO name`

Read-only inspection of a block definition. It reports:

- canonical definition name;
- base point;
- member count;
- number of live top-level references;
- member-kind counts;
- nested block-definition dependencies.

It does not change drawing revision or selection.

### `BLOCKREFS name`

Selects every live top-level reference to the requested block definition in deterministic handle order. The drawing database remains read-only.

### `BLOCKCLONE sourceName targetName`

Clones the source definition's base point and member drafts into a new definition. Existing target names are rejected atomically.

### `BLOCKPURGE`

Purges definitions that are unreachable from all live top-level block references.

Reachability is graph-aware: if a live block reference points to definition `A`, and `A` contains nested references to `B`, then both `A` and `B` are retained. Nested dependencies are traversed recursively. Definitions outside the reachable graph are erased in one transaction.

Safety rules:

- malformed nested block references fail closed;
- missing nested definitions fail closed;
- a fully reachable or empty block graph is a no-op and creates no database revision;
- an unreachable dependency cluster can be removed atomically in one revision.

### `BLOCKSET handle blockName x y scale rotationDegrees`

Updates one existing block reference without using generic `SETPROP` on reserved `QS3D.*` fields.

The command can change, in one transaction:

- target block definition;
- insertion X/Y;
- uniform scale;
- rotation in degrees.

Insertion Z is preserved from the existing reference because the standalone editor is currently a 2D product surface.

The command recomputes the block reference extents from every member-extents corner using the target definition base point, requested scale and rotation. Derived coordinates must remain finite.

User metadata already attached to the reference is preserved. Only the canonical block-reference structural properties are replaced.

If the requested definition/position/scale/rotation and derived extents already match the current reference, the command succeeds as a no-op without publishing a new drawing revision. Successful commands select the edited block reference.

## Transaction and history behavior

All mutating commands participate in the existing application command journal. Therefore block definition creation, cloning, purge, insertion, deletion and `BLOCKSET` can be undone/redone through the normal application `UNDO` / `REDO` workflow when their underlying transaction publishes a revision.

Validation happens before commit wherever possible. Missing handles, missing block definitions, invalid numbers, zero/negative scales, locked-layer reference edits and unsafe definition deletion fail without partial publication.

## Current scope boundary

This reference implementation intentionally does not claim:

- native DWG block table encoding;
- dynamic blocks, visibility states or stretch parameters;
- block attributes / attribute definitions;
- annotative scales;
- nested native graphics regeneration;
- native explode/refedit behavior;
- xref-as-block semantics;
- licensed AutoCAD/BricsCAD runtime qualification.

Those require separate adapter/runtime lanes rather than being inferred from the deterministic standalone block model.
