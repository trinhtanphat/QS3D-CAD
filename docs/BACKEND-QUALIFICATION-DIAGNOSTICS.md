# Production backend qualification diagnostics

`CadQualifiedBackendSelector.SelectProduction` remains the production gate for selecting a native CAD backend. It still requires an available native backend, explicit backend version, required backend capabilities, passing evidence for the exact backend ID/version/source SHA, and evidence capabilities covering the requested production capability set.

`CadQualifiedBackendSelector.EvaluateProduction` is a diagnostics-only companion. It evaluates the same input invariants and reports why each candidate does or does not qualify without weakening or bypassing the production selection policy.

## Diagnostic codes

Candidates are evaluated deterministically and receive one of these codes:

- `Unavailable`: the backend descriptor is unavailable; its declared unavailable reason is preserved.
- `NonNative`: the backend is a reference backend and cannot satisfy production-native qualification.
- `MissingVersion`: the native backend does not declare an exact version.
- `BackendCapabilityMismatch`: the backend itself lacks one or more required capabilities.
- `MissingEvidence`: no qualification evidence exists for the backend ID.
- `FailedEvidence`: evidence exists for the backend, but none of it passed.
- `VersionMismatch`: passing evidence exists, but not for the backend's exact declared version.
- `SourceMismatch`: passing exact-version evidence exists, but not for the requested exact source SHA.
- `EvidenceCapabilityMismatch`: passing exact-version/source evidence exists, but it does not qualify every required capability.
- `Qualified`: at least one passing exact backend/version/source evidence item covers all required capabilities.

For a qualified candidate, the evidence chosen for diagnostics uses the same deterministic ordering as production selection: newest `QualifiedAt`, then ordinal `EvidenceId`.

## Report ordering and selected candidate

The report orders candidates deterministically by backend kind, descending priority, then ordinal backend ID. The report's optional `Selection` is the first qualified native candidate in production priority order and therefore must match `SelectProduction` for the same inputs.

A report with `Selection == null` is a diagnosis of a failed qualification set. It is not permission to fall back to a reference backend or to ignore missing evidence.

## Validation and safety

`EvaluateProduction` rejects the same invalid/ambiguous inputs as production selection:

- unknown or empty required capability sets;
- malformed/non-exact source SHA;
- null candidate/evidence entries;
- duplicate backend IDs;
- duplicate evidence IDs.

Diagnostics do not create, transform, upgrade, or reinterpret qualification evidence. They cannot turn failed, wrong-version, wrong-source, or capability-incomplete evidence into a production PASS.

Repository CI/smoke can validate the policy and deterministic diagnostics contract, but it cannot manufacture licensed AutoCAD/BricsCAD/native runtime evidence. Native qualification remains tied to the evidence supplied to the selector.
