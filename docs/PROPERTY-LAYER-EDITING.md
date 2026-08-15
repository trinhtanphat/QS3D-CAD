# Reference property and layer editing

This document describes the standalone **reference/in-memory** entity metadata and layer-editing workflow. It is intentionally conservative so editing metadata cannot silently corrupt geometry or application-owned structural state.

## Commands

### `CHLAYER handle... layerName`

Moves one or more distinct entities to an existing editable layer in one transaction.

- Missing handles fail the whole batch.
- Locked or frozen source layers reject a real move.
- Locked or frozen target layers are rejected.
- Duplicate handles are deduplicated.
- If every object is already on the requested layer, the command succeeds as a no-op and does not create a drawing revision.
- Successful commands preserve the distinct source handles as the active selection.

### `SETPROP handle... key value`

Sets a **non-structural metadata property** on one or more entities in one transaction.

- Existing property-key casing is preserved (`User.Tag` updated through `user.tag` remains `User.Tag`).
- Multiple existing case variants are treated as ambiguous and fail closed.
- Missing handles and locked/frozen source layers fail the whole batch.
- Re-applying the same value to every requested entity is a no-op and does not create a drawing revision.
- Property keys are limited to 128 characters and values to 4096 characters.

### `DELPROP handle... key`

Deletes a non-structural metadata property from one or more entities transactionally.

- Matching is case-insensitive while preserving the actual stored key identity.
- Missing properties are per-entity no-ops.
- If no requested entity contains the property, no drawing revision is created.
- Missing handles and locked/frozen source layers fail atomically when a mutation would be required.

## Protected structural keys

The metadata commands reject geometry-owned keys such as `x1`, `y1`, `x2`, `y2`, `cx`, `cy`, and `radius` because changing those independently would desynchronize geometry and extents.

Application-owned `QS3D.*` entity properties are also reserved, including block-reference structure (`QS3D.BlockName`, insertion coordinates, scale and rotation). Those values must be changed through the feature that owns their geometry/semantic invariants, not generic metadata editing.

User metadata should use a non-reserved namespace such as `User.Tag`, `User.Code`, `Meta.Source`, or another project-specific prefix that does not begin with `QS3D.`.

## Desktop workflow

The entity-list context menu adds:

- **Move selected to current layer** — also available with `Ctrl+Shift+L`; executes `CHLAYER` immediately against the current layer.
- **Set metadata property...** — also available with `Ctrl+Shift+P`; prepares `SETPROP` in the command box using `User.Tag` / `Value` placeholders.
- **Delete metadata property...** — prepares `DELPROP` in the command box.

Prepared commands remain editable before Enter so names/values containing spaces can use the existing command-line quoting behavior.

## Transaction and journal guarantees

- Batch commands validate all requested handles and safety constraints before publishing the transaction.
- Failed commands do not publish partial entity changes.
- Successful mutations create one database revision for the batch and participate in the standalone application `UNDO` / `REDO` journal.
- No-op commands intentionally create no database revision and therefore do not add a journal entry.
- Selection changes are editor state; successful commands set selection to the distinct requested source handles.

## Fidelity boundary

This is standalone reference/editor behavior. It is not evidence of native DWG property palettes, native layer-table semantics, proprietary CAD database transactions, or licensed runtime qualification.
