# Work claim — QS3D product-family bootstrapper

- Status: `SOURCE_READY`
- Agent: `chatgpt-gpt56sol`
- Registered: `2026-09-07T13:13:00+07:00`
- Coordination baseline main SHA: `bc0232c0dacc4d48db120123a95d598dccdde56e`
- Claim landing / implementation baseline main SHA: `b3bd44202c9b5a771379735ecb00d7527f96010f`
- Coordination issue: `#47`
- Implementation PR: `#49`
- Implementation branch: `agent/chatgpt-gpt56sol/product-family-bootstrapper-20260907`
- Validated implementation source head: `5fc621f202d4864f66b1229f12c294b632b6b686`
- Exact implementation CI: `QS3D CAD CI` run `34092886217` / run #131 / job `101649956248` — `SUCCESS`

## Delivered scope

- isolated `.NET 8` `tools/QS3D.ProductBootstrapper` executable with no AutoCAD/BricsCAD managed SDK references;
- strict schema-1 local family manifest with fail-closed unknown/duplicate properties, exact product/repository binding, exact release URL identity, byte/SHA-256 bounds and safe package/install contracts;
- read-only Windows host discovery for AutoCAD 2021-2027 (`R24.0`-`R26.0`) and BricsCAD V25/V26;
- exact-generation package planning with no neighboring-version fallback and deterministic pending diagnostics;
- dry-run with zero downloader/installer calls;
- bounded package acquisition, trusted GitHub release redirect policy, exact length/SHA-256 verification and failed-download cleanup;
- shell-free EXE invocation and bounded staged ZIP publication with traversal/root/overwrite protection;
- deterministic coordinator/CLI reporting and fail-fast / continue-on-error behavior;
- self-contained Windows x64 family-bootstrapper packaging with packaged manifest and SHA-256 sidecars;
- dedicated family validation script and CI step, intentionally isolated from `scripts/validate.ps1` so open standalone PR #35 remains non-overlapping.

## TDD / recovery evidence

- RED head `858ebd7f65c46bba4726512f0bd1c7dd6bdfb778`, CI run `34090531196`: failed exactly because schema-1 accepted an unknown root property.
- Recovery run `34092719481` exposed a real package-stream lifetime defect: SHA verification attempted to reopen a `FileShare.None` destination before its write stream was disposed.
- Source recovery `5fc621f202d4864f66b1229f12c294b632b6b686` closes/flushed the download stream before digest verification.
- Exact source-head run `34092886217` then passed standalone authoritative validation, family validation, standalone installer smoke and family-bootstrapper package smoke.

## Release truth / native boundary

The checked-in initial family manifest intentionally keeps all vendor-host components disabled until exact durable release/install contracts exist:

- AutoCAD 2021-2027: pending integrated durable release; AutoCAD 2026 additionally retains explicit native runtime-matrix qualification.
- BricsCAD V25 preview `v0.1.0-preview.10316`: exact ZIP bytes/source are pinned, but automatic installation remains disabled until a published ZIP destination contract exists.
- BricsCAD V26: pending durable qualified public release asset.
- Standalone preview evidence is pinned but remains disabled as a family payload because it is not native-DWG-qualified production evidence.

Hosted CI proves bootstrapper source/build/package behavior only. It does not claim licensed AutoCAD/BricsCAD runtime PASS or native DWG fidelity.

## Collision / integration audit

Final source diff was audited against open PRs #21/#23/#25/#27/#29/#31/#33/#35/#37/#39/#41/#43/#45. The original overlap with #35 on `scripts/validate.ps1` was removed by moving family validation into `scripts/validate-family-bootstrapper.ps1`; final overlap is zero.

## Completion verdict

- `SOURCE IMPLEMENTATION: COMPLETE`
- `EXACT IMPLEMENTATION CI: GREEN`
- `READY_FOR_REVIEW: YES`
- `NATIVE HOST QUALIFICATION: PENDING_NATIVE / HOST-RELEASE OWNED`
- `MERGED TO MAIN: NO`

The claim update itself is documentation-only and must still be covered by the final PR-head CI before PR #49 is marked ready. Final integration to `main` requires separate explicit owner authorization.
