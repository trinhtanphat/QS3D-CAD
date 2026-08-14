# QS3D CAD continuation checkpoint — 2026-08-14

This checkpoint records source-side work only. It does **not** claim `BUILD_PASS`, `DWG_PASS`, `LOCAL_NATIVE_PASS` or production-native qualification.

## Source hardening landed in this continuation

- CAD is pinned to exact `QS3D-Platform` commit `cfb334f2b95feb31a6f5f8969b9b1666ffbfc7c6`, containing the audited netstandard2.0 compatibility fixes, conformance alignment and BOQ compatibility fix.
- The public command surface no longer exposes raw command execution or command instances. `StandaloneCommandCatalog` exposes names, registration and membership only; execution is owned by `StandaloneCadApplication` so database/semantic revision changes are journaled even when a command returns failure or throws after commit.
- `UNDO` / `REDO` are application-journal commands and are not registerable plugin command names. Invalid/null/blank plugin command names fail at the public catalog boundary.
- The command tokenizer preserves empty quoted arguments and Windows paths with backslashes; pre-tokenized integrations use `ExecuteCommand` instead of joining/reparsing shell arguments.
- Direct document-manager create/open/close is routed through application lifecycle callbacks so semantic workspace and undo/redo state are created/cleaned consistently.
- Desktop File Open/Save uses the `.qs3d` project-package path with validated previous-generation backup/recovery; raw bootstrap JSON remains an internal deterministic fixture.
- Desktop multi-document selection activates the selected drawing instead of displaying an inert string list.
- Standalone readiness includes live CAD-reference validation and reports `ORPHAN_HANDLE` for missing source/generated handles in the active drawing.
- `.qs3d` manifest parsing and payload declarations are fail-closed: malformed JSON, unexpected/missing payload declarations, wrong media types, hash/length mismatch and structurally invalid drawing payloads are rejected. Backup recovery covers a malformed drawing payload whose manifest hash/length were updated consistently.
- Bootstrap drawing loading normalizes corrupt JSON, invalid/null DTO members and invalid reconstructed state to `InvalidDataException` at the storage boundary.
- Derived coordinate overflow in drawing commands fails as a command result without publishing a transaction or writing non-finite coordinates.
- Native backend selection/qualification rejects duplicate backend/evidence identities, unknown capability bits, empty production capability requirements and passing evidence with zero qualified capabilities. Exact source SHA and backend version remain mandatory.
- CAD capability validation is aligned to the exact pinned Platform enum. `BooleanSolids` and `Grips` are accepted; the nonexistent `CustomEntities` symbol is forbidden by the standalone source gate.

## Shared Platform compatibility work

The Platform shared projects remain `netstandard2.0`. This continuation removed newer-BCL use from shared in-memory/BOQ paths and strengthened the boundary guard to reject APIs such as `ThrowIfNull`, `Dictionary.TryAdd`, `SHA256.HashData`, `Convert.ToHexString` and `CryptographicOperations.FixedTimeEquals` from shared Platform source.

## Source-gate status

The recovered stale command-registration gate has been fixed. The smoke now uses `StandaloneCommandCatalog.Contains`, verifies that public `Execute`/`TryResolve` do not reappear, and exercises reserved plus malformed command registrations. The standalone source-boundary gate now matches that contract and locks the exact pinned CAD capability names.

The CAD repository currently has no `.github/workflows` directory, so there is no CAD GitHub Actions build run to claim as validation. The authoritative source validation entry point remains `scripts/validate.ps1` on a machine with the required .NET toolchain and initialized Platform submodule.

## Evidence boundary

Platform GitHub Actions run `31760426233` for exact Platform commit `cfb334f2b95feb31a6f5f8969b9b1666ffbfc7c6` did not execute source steps: job `94645403253` has `runner_id=0`, `steps=[]`, and the check annotation states that the Actions budget prevented the job from starting. Therefore source/static review must not be relabeled as a build pass.

A licensed native CAD/DWG/rendering SDK, Windows graphics runtime and exact-SHA native qualification are still required for native DWG/viewport/snap/Xref/layout/plot/PDF/3D/installer claims. The ODA bootstrap remains discovery/configuration only and does not turn an SDK directory into production qualification.

## Coordination note

`QS3D-BricsCAD` remains read-only for this remote continuation while its repository claim search is incomplete. Its collaboration policy requires enumerating all `ACTIVE`/`BLOCKED` claims and publishing a non-overlapping claim before substantive source work; no remote source write should bypass that protocol.
