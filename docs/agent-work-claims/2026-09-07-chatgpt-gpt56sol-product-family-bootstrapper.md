# Work claim — QS3D product-family bootstrapper

- Status: `ACTIVE`
- Agent: `chatgpt-gpt56sol`
- Registered: `2026-09-07T13:13:00+07:00`
- Baseline main SHA: `bc0232c0dacc4d48db120123a95d598dccdde56e`
- Coordination issue: `#47`
- Implementation branch: `agent/chatgpt-gpt56sol/product-family-bootstrapper-20260907`
- Integration batch: `TBD`

## Reserved scope

Distribution-only bootstrapper/orchestrator for the QS3D product family. It may detect installed AutoCAD/BricsCAD hosts, resolve a versioned pinned component manifest, verify downloaded package integrity, and invoke/stage independently produced host installers/packages. It must preserve `QS3D-CAD` as a fully standalone product.

## Expected surfaces

- new isolated bootstrapper project under `tools/` or `installer/`;
- versioned product-family manifest schema + checked-in test fixture;
- host discovery abstractions and Windows registry implementation;
- download/integrity/invocation orchestration;
- deterministic dry-run and local-fixture tests;
- focused distribution/bootstrapper documentation;
- CI wiring required to build/test/package the bootstrapper without licensed CAD.

## Explicit exclusions

- no BricsCAD or AutoCAD SDK/plugin DLLs committed to `QS3D-CAD`;
- no runtime dependency from `QS3D.exe` / standalone host into BricsCAD or AutoCAD;
- no mutation of `QS3D-BricsCAD`, `QS3D-AutoCAD`, `QS3D-Platform`, or `QS3D-CAD-MCP` in this claim;
- no claim of native AutoCAD/BricsCAD PASS from hosted CI;
- no vendor-license bypass, unsigned-binary trust bypass, or arbitrary unpinned `latest` execution;
- no unrelated standalone CAD feature work and no collision with open PRs #21/#23/#25/#27/#29/#31/#33/#35/#37/#39/#41/#43/#45.

## Validation plan

- RED/GREEN regression coverage for manifest validation, host detection mapping, package selection, digest mismatch, duplicate/ambiguous assets, dry-run no-mutation, child-process failure aggregation and cleanup;
- exact task-head `QS3D CAD CI` green;
- bootstrapper build/package smoke on Windows x64;
- existing authoritative standalone validation and existing `QS3D-CAD` installer smoke remain green;
- final diff audit confirms no host SDK binaries and no standalone runtime dependency on vendor hosts.

## Completion condition

Source/CI lane is complete when a reviewable exact-head PR contains a deterministic distribution-only bootstrapper and all applicable hosted gates are green. Native host installation/runtime qualification and any final merge to `main` remain separate owner-authorized gates.
