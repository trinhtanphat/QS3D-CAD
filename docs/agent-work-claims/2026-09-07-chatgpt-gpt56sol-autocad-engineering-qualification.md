# Work claim — AutoCAD engineering qualification manifest

- Status: `ACTIVE`
- Agent: `chatgpt-gpt56sol`
- Registered: `2026-09-07T14:43:00+07:00`
- Baseline main SHA: `68f7633b2dc797ea22a371fa5a6fdcad24ddc40a`
- Claim branch: `claim/chatgpt-gpt56sol/autocad-engineering-qualification-20260907`
- Implementation branch: `agent/chatgpt-gpt56sol/autocad-engineering-qualification-20260907`
- Integration batch: `TBD`

## Reserved scope

Add a non-production engineering qualification manifest for the exact AutoCAD post-merge CI candidate `test-v0.1.0-ci.266` from `QS3D-AutoCAD@8dd65a7e5061430f76e027467261beb8f18c69a8`, and harden the production family manifest/guards so engineering prereleases can never become production-enabled accidentally.

## Expected surfaces

- `installer/product-family.manifest.json`
- new engineering-only manifest under `installer/`
- `scripts/validate-family-bootstrapper.ps1`
- bootstrapper smoke/source guards as needed
- focused docs describing engineering-vs-production qualification

## Exact engineering evidence to pin

- repository: `trinhtanphat/QS3D-AutoCAD`
- tag: `test-v0.1.0-ci.266`
- source SHA: `8dd65a7e5061430f76e027467261beb8f18c69a8`
- Setup asset: `QS3D-AutoCAD-0.0.0-ci-Setup.exe`
- Setup SHA-256: `9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de`
- Setup bytes: `67998359`
- qualification: engineering/native-test candidate only; unsigned and not native PASS

## Excluded scope

- no production component is enabled by this lane;
- no signed commercial release is published;
- no AutoCAD native PASS is manufactured from hosted CI;
- no BricsCAD V25/V26 production enablement;
- no changes to standalone runtime or vendor plugin binaries.

## Validation plan

- TDD RED first: production validation must fail if an enabled component references a `test-v*` release or engineering qualification;
- GREEN: add strict engineering manifest plus production fail-closed guard;
- exact branch CI must pass standalone authoritative validation, family bootstrapper validation, installer/package smoke;
- final PR head must be mergeable and drift-audited against current `main`.

## Completion condition

Engineering AutoCAD 2021-2027 test candidate is exactly pinned for native qualification while the production family manifest remains fully fail-closed and no test prerelease can be production-enabled.
