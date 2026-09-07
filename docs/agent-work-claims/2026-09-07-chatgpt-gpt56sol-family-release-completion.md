# Work claim — family release completion

- Status: `RELEASED`
- Agent: `chatgpt-gpt56sol`
- Registered: `2026-09-07T16:22:00+07:00`
- Baseline main SHA: `982e9d651e33bab47e3b4dbfeb5fbb5f6b174678`
- Coordination issue: `#53`
- Implementation branch: `agent/chatgpt-gpt56sol/family-release-completion-20260907`
- Recovery branch: `recovery/chatgpt-gpt56sol/family-release-trigger-20260907`
- Integration batch: `integration/family-release-completion-20260907`
- Main implementation PR: `#55`
- Release-trigger recovery PR: `#56`
- Final integrated release-source main SHA: `e7509af2588fa3a967ab3ab89a55d03a0a9e8931`
- Exact-main CI: `QS3D CAD CI` run `34110633250` / run #163 — `SUCCESS`
- Published family release workflow: run `34110913546` / run #1 — `SUCCESS`
- Published tag: `family-v0.1.0-preview.5`
- Published release source SHA: `e7509af2588fa3a967ab3ab89a55d03a0a9e8931`

## Reserved scope

Complete the QS3D-CAD product-family release/distribution lane without weakening native/signing gates: family Windows release workflow, exact-source release verification, production/engineering family manifest provenance and release packaging, operator-facing native qualification entrypoint/documentation, and family release tests/guards.

## Delivered release contract

- dedicated QS3D Family Windows prerelease workflow with exact-source identity and ancestry verification;
- deterministic tag `family-v$VERSION` with same-tag/different-source rejection;
- manual `workflow_dispatch` path plus narrow `family-release/**` push path for tool-accessible publication; both resolve an exact source SHA and pass the same identity/package/release gates;
- exact six-asset release set:
  - `QS3D-Family-Setup-win-x64.exe`
  - `QS3D-Family-Setup-win-x64.exe.sha256`
  - `product-family.manifest.json`
  - `product-family.manifest.json.sha256`
  - `product-family.engineering.manifest.json`
  - `product-family.engineering.manifest.json.sha256`
- pre-publication SHA-256 sidecar verification and post-publication asset-count/name/size/digest verification;
- AutoCAD 2021-2027 native qualification handoff pinned to the exact engineering candidate without manufacturing native PASS;
- BricsCAD V25/V26 release/native prerequisites documented and production vendor-host entries kept fail-closed.

## TDD / integration evidence

- Main lane RED head `858bb6bf836604c4e7e0bcea73a9f5ef3d39086a`, CI run `34106718352`: authoritative validation passed and family validation failed only because the new release/handoff surfaces were intentionally absent.
- Main lane final PR-head `12846463f2bdcb1a8d52c1178d6abaac326cfe80`, CI run `34107960356` / #156: full green.
- PR #55 merged release tooling/native handoff; post-merge CI run `34108307505` / #157: full green.
- Recovery RED head `2579ff59147471ab80a4f3bb3c1f5fea42eb41a8`, CI run `34108564224` / #158: authoritative validation passed and family validation failed only on the missing narrow branch-trigger contract.
- Recovery final clean head `c9dd657bc0203cdb31448195c9b917efeaf63631`, CI run `34110345536` / #162: full green; final diff contained only the family release workflow and its guard.
- PR #56 merged as `e7509af2588fa3a967ab3ab89a55d03a0a9e8931`.
- Exact-main CI run `34110633250` / #163: authoritative validation, family validation, standalone installer smoke, family package smoke and retained artifacts all succeeded.
- Release branch `family-release/family-v0.1.0-preview.5` was created directly at `e7509af2588fa3a967ab3ab89a55d03a0a9e8931` with no intermediate commit.
- Family release run `34110913546` succeeded through identity, package, artifact upload, GitHub prerelease publication and final published-release verification.

## Published release verification

Independent GitHub API verification confirmed:

- release `family-v0.1.0-preview.5` is a prerelease;
- `target_commitish` is exactly `e7509af2588fa3a967ab3ab89a55d03a0a9e8931`;
- Git tag `refs/tags/family-v0.1.0-preview.5` points directly to the same commit;
- exactly six assets are present and each exposes a GitHub SHA-256 digest;
- executable asset `QS3D-Family-Setup-win-x64.exe` size is `67535541` bytes with digest `sha256:298f7431731ff0d0b71e019f6195a7d997ffe824c5a233546453600920ef0526`;
- production and engineering manifests plus their SHA-256 sidecars are present with no extra release asset.

## Native / production boundary

Hosted GitHub CI and this family preview do **not** constitute licensed AutoCAD or BricsCAD native acceptance. AutoCAD/BricsCAD production family components remain disabled until their exact native evidence, signing, durable release and install-contract requirements are satisfied.

## Completion verdict

- `SOURCE / RELEASE TOOLING: COMPLETE`
- `MERGED TO MAIN: YES`
- `EXACT-MAIN CI: GREEN`
- `FAMILY PREVIEW PUBLISHED: YES`
- `RELEASE SOURCE / TAG / ASSETS VERIFIED: YES`
- `NATIVE HOST QUALIFICATION: PENDING_NATIVE / HOST-RELEASE OWNED`
- `SESSION CAN BE CLOSED/DELETED: YES`

This claim is terminal. Future native-host qualification or production vendor-package enablement must use a separate claim.
