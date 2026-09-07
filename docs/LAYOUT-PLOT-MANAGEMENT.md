# Layout and reference plot management

This document describes the standalone reference-adapter layout and plot-request surface in QS3D-CAD.

## Scope

The pinned QS3D-Platform adapter provides in-memory layout state and a reference plot service. The plot service records requests for validation and workflow parity; it does not generate a PDF, invoke a printer, or claim native DWG plotting fidelity.

The lifecycle state is document-scoped through `InMemoryAdvancedServicesRegistry`.

## Legacy compatibility

`LAYOUTREF` remains available with:

- `LAYOUTREF LIST`
- `LAYOUTREF CREATE name`
- `LAYOUTREF SET name`
- `LAYOUTREF DELETE name`

`PLOTREF layout targetPdfPath` remains available and records a PDF reference request.

Both legacy commands previously advertised `ReadOnly` even though they can mutate reference-adapter state. They now expose only `RequiresDocument`. The explicit commands below carry precise read-only versus mutation metadata.

## Explicit layout commands

### `LAYOUTLIST`

Lists every layout in deterministic service order, including model/paper dimensions and the current-layout marker.

### `LAYOUTCURRENT`

Reports the current layout and its reference snapshot.

### `LAYOUTCREATE name`

Creates a paper layout using the pinned Platform adapter defaults. The current adapter creates a 210 x 297 mm paper layout.

### `LAYOUTSET name`

Makes an existing layout current.

### `LAYOUTDELETE name`

Deletes a non-model, non-current layout. The Platform adapter rejects deletion of `Model` and rejects deletion of the current layout.

### `LAYOUTHEALTH`

Reports layout totals, the number of model/paper layouts, the current layout, and whether the current layout resolves to a live layout snapshot.

## Explicit plot-request commands

### `PLOTREQUEST layout target [Pdf|Printer]`

Records one reference plot request. `Pdf` is the default target kind.

A successful command means the request was accepted into the reference plot service. It does **not** mean a file was written or a printer job was submitted.

### `PLOTLIST`

Lists recorded reference plot requests in insertion order with deterministic 1-based request numbers, layout, target kind, target, and page-setup field.

### `PLOTHEALTH`

Reports total/PDF/printer request counts and counts requests whose referenced layout no longer exists.

Recorded requests are intentionally retained if a layout is later deleted; `PLOTHEALTH` surfaces those requests as `orphanedLayoutRequests` instead of silently discarding historical reference-service evidence.

## Command metadata

Read-only diagnostics use:

- `RequiresDocument | ReadOnly`

Reference-service lifecycle/request mutations use:

- `RequiresDocument | ModifiesDrawing`

The standalone application journal currently keys undo/redo to drawing database and semantic workspace revisions. Reference layout and plot-service mutations do not increment the drawing database revision and are not claimed to participate in application undo/redo.

The legacy mixed-operation wrappers use `RequiresDocument` only because one static command flag cannot truthfully describe both read-only and mutation subcommands.

## Isolation and failure semantics

Each standalone document receives its own advanced-services instance. Layouts and recorded plot requests do not leak between documents.

Failed operations are fail-closed at the reference-service boundary:

- duplicate layout creation does not replace an existing layout;
- missing layout set/delete operations fail;
- deleting `Model` fails;
- deleting the current layout fails;
- a plot request for a missing layout is not recorded;
- an unknown plot target kind is not recorded.

## Fidelity boundary

This lane does not claim:

- native DWG layout object persistence;
- paper-size or page-setup mutation APIs;
- CTB/STB handling;
- viewport-on-layout authoring;
- plot style/device discovery;
- PDF generation;
- printer/spooler submission;
- background/native plot progress;
- licensed SDK plotting parity.

Those require a qualified native backend and separate runtime evidence.
