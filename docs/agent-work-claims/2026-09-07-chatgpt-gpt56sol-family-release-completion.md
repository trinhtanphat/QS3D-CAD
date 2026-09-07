# Work claim — family release completion

- Status: `ACTIVE`
- Agent: `chatgpt-gpt56sol`
- Registered: `2026-09-07T16:22:00+07:00`
- Baseline main SHA: `982e9d651e33bab47e3b4dbfeb5fbb5f6b174678`
- Coordination issue: `#53`
- Implementation branch: `agent/chatgpt-gpt56sol/family-release-completion-20260907`
- Integration batch: `integration/family-release-completion-20260907`

## Reserved scope

Complete the QS3D-CAD product-family release/distribution lane without weakening native/signing gates: family Windows release workflow, exact-source release verification, production/engineering family manifest provenance and release packaging, operator-facing native qualification entrypoint/documentation, and family release tests/guards.

## Expected surfaces

- `.github/workflows/*family*release*.yml` or one new dedicated family release workflow;
- `scripts/package-family-bootstrapper.ps1` and narrowly scoped family-release verification/qualification scripts;
- `installer/product-family.manifest.json` and `installer/product-family.engineering.manifest.json` only for truthful provenance/qualification metadata, never to manufacture production PASS;
- `docs/PRODUCT-FAMILY-INSTALLER.md` and a focused release/native handoff document if needed;
- `scripts/validate-family-bootstrapper.ps1` and focused deterministic guards/tests;
- this claim file.

## Excluded scope

- AutoCAD/BricsCAD vendor runtime implementation;
- synthetic or hosted-CI native PASS;
- bypassing AutoCAD `QS3D_NATIVE_ACCEPTED_SHA` / Authenticode release gates;
- enabling production AutoCAD/BricsCAD packages before exact native + durable signed release/install evidence exists;
- unrelated standalone CAD feature PRs.

## Validation plan

- TDD RED -> GREEN for release asset/source-identity and fail-closed production-enablement guards;
- existing authoritative QS3D-CAD validation remains green;
- family bootstrapper validation/build/smoke remains green;
- family package produces executable, both manifests and exact SHA-256 sidecars;
- release workflow dry/source guard proves exact SHA/tag/assets and refuses ambiguous replacement;
- final exact PR-head CI, current-main drift audit, reviewed integration landing and exact-main CI;
- after source landing, publish one family preview from the exact main SHA and verify published assets/checksums/source identity.

## Completion condition

Source/release tooling is integrated and exact-main green; a QS3D Family preview is publicly published from the exact integrated main SHA with verified executable/manifests/checksums; native qualification remains truthfully external/PENDING until real licensed-host evidence is supplied. Production vendor-host entries remain fail-closed until their native/signing/durable-release gates are actually satisfied.
