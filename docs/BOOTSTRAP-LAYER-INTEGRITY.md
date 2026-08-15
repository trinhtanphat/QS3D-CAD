# Bootstrap drawing layer integrity

QS3D bootstrap schemas 3 and 4 persist an explicit layer table and current-layer state. Modern bootstrap loading therefore treats layer ownership as part of the serialized drawing contract rather than reconstructing missing layer state implicitly.

## Modern schema rules

For schema 3 and newer:

- every serialized entity must contain a nonblank `LayerName`;
- every entity layer reference must resolve case-insensitively to a layer declared by the serialized `Layers` table;
- the existing current-layer requirement remains in force.

For schema 4 block definitions:

- every persisted block member must contain a nonblank `LayerName`;
- every block-member layer reference must resolve to a layer declared by the serialized `Layers` table.

If a modern file violates these invariants, `BootstrapDrawingStore.LoadWithProject` fails closed with `InvalidDataException`. It does not silently move the object to layer `0` and does not rely on the in-memory database's generic snapshot normalization to manufacture an undeclared layer.

## Legacy compatibility

Schemas 1 and 2 predate persisted layer state. Their entity records may omit `LayerName`; the loader continues to map a missing legacy entity layer to `0`. This compatibility behavior is intentionally limited to pre-layer schemas.

The pinned Platform's generic `CadEntityDraft.LayerName` remains nullable for authoring APIs. This persistence hardening does not change that API contract. Current database snapshots normalize block-member ownership before serialization, so schema-4 persistence still writes explicit member layer names.

## Validation

Deterministic smoke coverage verifies:

- blank schema-4 entity layer is rejected;
- undeclared schema-4 entity layer is rejected;
- blank schema-4 block-member layer is rejected;
- undeclared schema-4 block-member layer is rejected;
- schema-2 missing entity layer still loads as layer `0`;
- a valid schema-4 entity/block graph with declared layer ownership still loads unchanged.

This is bootstrap/reference-format integrity. It does not claim native DWG layer-table encoding or proprietary CAD database recovery fidelity.
