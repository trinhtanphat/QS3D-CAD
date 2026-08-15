# Backend qualification evidence JSON integrity

Schema-1 qualification evidence can participate in production backend selection, so the JSON codec treats the document shape as part of the trust boundary.

## Canonical schema

The root contains exactly these logical properties:

- `Schema`
- `Items`

Each schema-1 evidence item contains exactly these logical properties:

- `BackendId`
- `BackendVersion`
- `SourceSha`
- `QualifiedCapabilities`
- `QualifiedAt`
- `EvidenceId`
- `Passed`

Property matching remains case-insensitive for compatibility with the existing codec, but a logical property may occur only once. For example, a document containing both `Passed` and `passed` is rejected as ambiguous rather than allowing JSON overwrite semantics to choose one value.

Unknown root/item properties are rejected under schema 1. Extending the evidence format therefore requires an explicit schema evolution rather than silently attaching fields that current production qualification does not understand.

Every root/item property above is required. This prevents missing value-type fields such as `Passed` or `QualifiedAt` from being silently supplied by DTO default values before the evidence model validates the record.

## Validation sequence

`CadBackendQualificationEvidenceJson.Deserialize` now validates JSON structure before DTO deserialization:

1. the root must be an object;
2. required root properties must each appear once and no unknown root fields may appear;
3. `Items` must be an array;
4. every item must be an object containing each required logical field exactly once and no unknown fields;
5. DTO deserialization then validates JSON value types;
6. `CadBackendQualificationEvidence` continues to validate backend ID/version, exact 40-character source SHA, capability flags and evidence ID;
7. duplicate evidence IDs remain rejected.

Malformed, incomplete or ambiguous schema-1 documents fail closed with `InvalidDataException`.

## Non-goals

This hardening does not create or upgrade qualification evidence, does not reinterpret a failed evidence record as passing, and does not replace licensed/native runtime qualification. It only ensures that evidence JSON entering the existing production qualification pipeline has one unambiguous schema-1 interpretation.
