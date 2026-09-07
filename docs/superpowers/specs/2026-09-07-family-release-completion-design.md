# QS3D Product-Family Release Completion Design

Date: 2026-09-07
Issue: #53
Baseline after claim landing: `09089a00635f2b0082828c23cf02af4587ce2b8f`

## Goal

Complete the remaining QS3D product-family distribution/release path without weakening the existing evidence boundary. The source-side AutoCAD integration and family bootstrapper already exist on `main`; this change makes the family bootstrapper publishable as a durable GitHub preview release, keeps production vendor-host installation fail-closed until exact native/signing evidence exists, and makes native qualification operationally straightforward against the exact pinned engineering AutoCAD candidate.

This design intentionally distinguishes three meanings of "complete":

1. **Source/release-tooling complete** — family release workflow, packaging, verification, documentation and CI are integrated and exact-main green.
2. **Preview-distribution complete** — a public QS3D Family preview release exists from an exact integrated `QS3D-CAD/main` SHA with verified assets/checksums.
3. **Production vendor-host qualified** — AutoCAD/BricsCAD components are enabled only after their repository-owned native/signing/durable-release evidence is actually satisfied. This cannot be manufactured by hosted CI.

## Existing state

- `QS3D-AutoCAD/main` is integrated at `8dd65a7e5061430f76e027467261beb8f18c69a8` and exact-main CI is green.
- AutoCAD engineering release `test-v0.1.0-ci.266` is pinned for 2021-2027 native testing; it is unsigned and explicitly not native PASS.
- `QS3D-CAD` family bootstrapper already packages:
  - `QS3D-Family-Setup-win-x64.exe`;
  - `product-family.manifest.json`;
  - `product-family.engineering.manifest.json`;
  - SHA-256 sidecars for all three.
- `QS3D-CAD` existing Windows release workflow publishes only the standalone `QS3D-CAD-Setup-win-x64.exe` payload.
- Production manifest AutoCAD 2021-2027, BricsCAD V25/V26 and standalone entries remain disabled for documented qualification reasons.

## Architecture

### 1. Dedicated family release workflow

Add a dedicated GitHub Actions workflow, separate from the standalone `release-windows.yml` workflow. The family workflow owns only distribution of the already-existing product-family bootstrapper package.

The workflow supports explicit `workflow_dispatch` with exactly:

- `confirm_release=RELEASE`;
- exact lowercase 40-character `source_sha`.

The family release tag is deterministic and has no free-form tag input: it is exactly `family-v$VERSION`, where `VERSION` is the checked-in release-safe version from the exact source commit. With the current `VERSION=0.1.0-preview.5`, the intended first family tag is `family-v0.1.0-preview.5` unless `VERSION` changes through a separately reviewed source change before publication.

The workflow must checkout the exact source SHA, require that source to be reachable from current `origin/main`, validate `VERSION`, run existing authoritative validation plus family validation, invoke `scripts/package-family-bootstrapper.ps1`, verify all generated sidecars, then publish a GitHub prerelease.

The release must never silently reuse or overwrite an existing tag that points at a different source SHA. Existing same-tag/same-source preview assets may be refreshed only if their prerelease state and source identity match exactly.

### 2. Family release asset contract

Every family preview release contains exactly six user-consumable family distribution assets:

- `QS3D-Family-Setup-win-x64.exe`;
- `QS3D-Family-Setup-win-x64.exe.sha256`;
- `product-family.manifest.json`;
- `product-family.manifest.json.sha256`;
- `product-family.engineering.manifest.json`;
- `product-family.engineering.manifest.json.sha256`.

This lane does not add a seventh provenance JSON file. Exact release provenance is represented by the immutable source/tag check plus the six assets and their SHA-256 sidecars. Vendor package provenance remains inside the manifests themselves.

The workflow must fetch its own published release metadata after upload and verify tag identity, prerelease state and the exact six required asset names. Where GitHub exposes asset digests, the workflow compares those to local SHA-256 values; otherwise the checked-in sidecar content and exact-source package build remain the integrity evidence.

### 3. Production manifest remains fail-closed

`installer/product-family.manifest.json` remains the default manifest shipped beside the family bootstrapper.

Rules:

- No `test-v*` AutoCAD engineering release may appear as an enabled production component.
- AutoCAD 2021-2027 remain disabled until a signed durable AutoCAD release exists from a source SHA accepted by the repository's native release gate.
- AutoCAD 2026 retains the explicit CLR 8-or-10 native matrix boundary.
- BricsCAD V25 remains disabled until both native qualification and a durable install destination/strategy contract are available for the published asset.
- BricsCAD V26 remains disabled until a durable qualified release asset and install contract exist.
- Standalone remains disabled in the family production manifest until it is intentionally promoted as a current product-family payload; the existing standalone release workflow remains independent.

Hosted CI may validate these rules but may not convert a disabled component to enabled merely because source/build/package tests pass.

### 4. Engineering manifest remains non-production

`installer/product-family.engineering.manifest.json` continues to pin the exact AutoCAD CI #266 candidate for 2021-2027.

Engineering entries may remain `enabled=true` because the manifest is explicitly selected for licensed-host qualification. Every entry must continue to carry a qualification string containing both engineering/non-production intent and `not-native-pass` truth.

The release workflow publishes this manifest beside the production manifest so a tester can use the public family preview as the native-test orchestrator without editing files or scraping release URLs.

### 5. Manifest provenance semantics

Clarify `generatedFromSource` so it never implies something false.

For the production family manifest, the field represents the `QS3D-CAD` source commit that last changed the family manifest state, not the vendor package source. Vendor exact source remains in each component's `sourceSha`.

For the engineering manifest, `generatedFromSource` continues to identify the vendor engineering candidate source because existing guard/schema behavior already treats it as candidate provenance. This lane does not change schema 1. Documentation must explicitly distinguish these two semantics so the same field is not misread as cross-repository source identity.

The production manifest should refresh its currently stale `generatedFromSource` value to the exact implementation source commit that changes its qualification/provenance state. The engineering manifest remains pinned to AutoCAD candidate source `8dd65a7e5061430f76e027467261beb8f18c69a8`.

### 6. Native qualification operator entrypoint

Do not duplicate AutoCAD's native evidence implementation inside QS3D-CAD. `QS3D-AutoCAD` remains authoritative for `new-native-acceptance.ps1`, runtime/result recorders and final validator.

QS3D-CAD provides one operator-facing family handoff script that:

1. accepts one exact AutoCAD generation from 2021 through 2027;
2. loads `installer/product-family.engineering.manifest.json` through deterministic JSON validation or a narrowly equivalent source-safe check;
3. verifies the selected component pins CI #266 source/tag/asset/hash/bytes exactly;
4. optionally runs the family bootstrapper in `--dry-run` mode;
5. prints the exact `QS3D-AutoCAD` repository commands needed to create/record/validate that generation's native evidence;
6. never writes a passing evidence result itself;
7. exits nonzero for missing/disabled/mismatched engineering component metadata.

The script may accept an optional path to a local `QS3D-AutoCAD` checkout. When supplied, it may verify that the expected native-acceptance scripts exist and print paths relative to that checkout, but it must not mark checks PASS automatically. When the checkout is absent, behavior remains read-only/instructional.

The operator workflow explicitly covers AutoCAD 2021, 2022, 2023, 2024, 2025, 2026 and 2027 individually. Passing one legacy R24.x host must not be represented as passing the other legacy generations.

### 7. BricsCAD handoff

Document, rather than invent, the remaining BricsCAD gates in this lane.

For V25, the family manifest already pins preview `v0.1.0-preview.10316`. Production enablement additionally requires:

- exact native BricsCAD V25 acceptance for the chosen release source;
- a durable public release asset intended for family distribution;
- an explicit safe installation destination/strategy contract for the ZIP payload;
- verified asset bytes/SHA-256 in the production manifest.

For V26, require the analogous durable release, exact native evidence and install contract before enabling.

Add fail-closed family validation that refuses any enabled BricsCAD ZIP component without explicit destination metadata. It must not invent a destination from assumptions about BricsCAD installation directories.

### 8. Tracker hygiene

Update AutoCAD native tracker metadata/comments only to eliminate stale source-status ambiguity:

- #52: source is merged through #85; tracker remains open for native UI/runtime evidence.
- #58: source is merged through #85; tracker remains open for native MEP evidence.
- #60: source is merged through #85; tracker remains open for native review/profile evidence.

Do not close these issues until their real licensed-host acceptance criteria are satisfied.

Issue #53 owns only family release/distribution tooling and should close when the family preview is published and exact-main verification is complete, even if vendor native trackers remain open.

## Release naming

The family release namespace is exactly:

`family-v<contents-of-VERSION>`

Examples:

- `VERSION=0.1.0-preview.5` -> `family-v0.1.0-preview.5`;
- `VERSION=0.1.0` -> `family-v0.1.0`.

The family workflow is not triggered by generic `v*` tags, because that namespace belongs to the standalone release workflow. The first implementation uses explicit `workflow_dispatch`; no automatic family tag trigger is required in this lane.

Immediately before publication, the workflow checks whether `family-v$VERSION` already exists. Same tag with a different source is a hard failure. Same tag and same source may refresh the six assets only after prerelease-state verification.

## Testing strategy

Follow TDD for behavior changes.

### RED gates

Add deterministic source validation that initially fails because the dedicated family release workflow is absent. The guard must require:

- exact-source checkout / ancestry validation;
- explicit release confirmation for manual dispatch;
- deterministic `family-v$VERSION` tag construction;
- dedicated family package invocation;
- all six required assets;
- same-tag source-identity protection;
- post-publication verification;
- no generic standalone-release asset substitution.

Add a second fail-closed guard for manifest/native boundaries: reject enabled production AutoCAD components that reference engineering tags and reject enabled BricsCAD ZIP components without explicit destination contract.

### GREEN gates

The implementation is green only when:

- existing `scripts/validate.ps1` passes unchanged in intent;
- `scripts/validate-family-bootstrapper.ps1` passes;
- family bootstrapper build/C# smoke passes;
- family package smoke produces executable + both manifests + three SHA sidecars;
- family release workflow source guard passes;
- the qualification handoff script has deterministic no-host smoke coverage for all seven AutoCAD generations and mismatch rejection;
- exact PR-head CI is successful.

### Integration and publication

After explicit main-integration authorization:

1. refresh current `main` and verify no overlap/drift;
2. merge the final carrier with expected head SHA;
3. require exact-main CI success;
4. dispatch the dedicated family release workflow with `confirm_release=RELEASE` and that exact main SHA;
5. verify published tag `family-v$VERSION` targets the same SHA;
6. verify the exact six release assets and SHA sidecars;
7. record release tag/source/digests in issue #53 and terminal claim closeout.

## Error handling and safety

- Release workflow fails closed on malformed source SHA, wrong confirmation token, non-main ancestry, unsafe `VERSION`, tag/source mismatch, missing/extra required family asset, hash mismatch or prerelease-state mismatch.
- No release step mutates vendor repositories or GitHub native-acceptance variables.
- No workflow reads or prints signing secrets from AutoCAD/BricsCAD repositories.
- Production manifests remain disabled when evidence is incomplete.
- Engineering release URLs remain restricted to trusted GitHub release hosts by the existing bootstrapper trust model.
- Existing standalone `release-windows.yml` remains behaviorally independent.

## Definition of Done

This lane is complete when all of the following are true:

- claim is visible and non-overlapping;
- design/spec and implementation plan are committed;
- implementation branch is TDD-green on exact head;
- reviewed final PR is integrated to current `main`;
- exact-main CI is green;
- public release `family-v$VERSION` is published from that exact main SHA;
- the release contains exactly the six required family assets with valid sidecars;
- issue #53 records exact release/source evidence and is closed completed;
- claim is terminal `COMPLETED` with exact-main CI and release evidence;
- AutoCAD #52/#58/#60 remain open only for real native evidence;
- production AutoCAD/BricsCAD enablement remains `NO` until the external licensed-host/signing/durable-release gates actually pass.

The overall product-family source/distribution lane may then be called 100% complete. The separate statement "AutoCAD/BricsCAD production native qualification is 100% complete" is permitted only after the vendor-native trackers themselves have real PASS evidence.
