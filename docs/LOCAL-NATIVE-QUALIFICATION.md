# Local native qualification lane

This file defines work that is allowed to be delegated to local agents with licensed/native CAD SDKs and real graphics/runtime access. It also defines work that local agents must not take from repo-only agents.

## Local-only work

A task belongs to the local/native lane only when repository source inspection is insufficient and at least one of these is required:

- legally licensed native CAD/DWG SDK binaries or runtime;
- real DWG customer/reference files and round-trip comparison in external CAD products;
- a real Windows graphics device, DPI/display stack, GPU driver, viewport/device lifecycle or native redraw behavior;
- native geometry/topology kernel behavior such as exact intersections, tangency, perpendicular projection, B-Rep topology or boolean operations;
- native Xref resolution, layout/page setup, printing, PDF generation or device configuration;
- installer/signing/clean-machine startup/runtime dependency qualification;
- crash/hang reproduction that requires BricsCAD, AutoCAD, BLT3D, licensed ODA/runtime components, or another installed native host.

## Local agents must not take these lanes

Local agents must not independently run/fix general repository CI, rewrite host-neutral Platform code, change ordinary source guards, modify documentation-only gates, or duplicate an active remote-agent claim merely because a local machine can run the code.

Repo-only build/source failures remain remote/repository-agent work unless the failure itself proves that a licensed/native dependency is required.

## Required qualification identity

Every local qualification result must identify:

- exact 40-character QS3D-CAD product source SHA;
- exact native backend ID and backend version/build;
- exact Platform gitlink SHA used by that product SHA;
- required and actually qualified capability set;
- OS/runtime/SDK/CAD product version;
- evidence ID and execution timestamp;
- PASS/FAIL and the exact failing operation when not PASS.

A PASS from another product SHA, another backend version, or a subset of required capabilities must not qualify the current production build.

## Minimum native gates before production claim

Production standalone qualification requires evidence for the capabilities actually shipped, including as applicable:

1. DWG open/save/save-as and round-trip fidelity on a representative corpus.
2. Transaction/undo/redo behavior against the native drawing database.
3. Real viewport device startup, pan/zoom/orbit, invalidation, resize and DPI behavior.
4. Native selection/hit testing and supported object snaps.
5. Xref attach/reload/unload/detach on real files.
6. Layout/page setup and native PDF/printing output verification.
7. Large-coordinate and large-drawing stability/performance.
8. Native 3D solid/topology/boolean behavior when those capabilities are shipped.
9. Installer/signing/clean-machine startup and dependency discovery.
10. Crash recovery and `.qs3d`/DWG persistence interaction where native drawing payloads are integrated.

## Evidence vocabulary

Use these terms consistently:

- `SOURCE_READY`: source and deterministic regressions are authored; no build/runtime proof implied.
- `BUILD_PASS`: exact source built successfully in the stated toolchain.
- `REFERENCE_PASS`: deterministic reference/in-memory behavior passed; no native claim.
- `LOCAL_NATIVE_PASS`: required native capability passed on the exact product/backend versions stated in evidence.
- `PRODUCTION_QUALIFIED`: all required release capabilities have exact-SHA/version evidence and release gates pass.

`REFERENCE_PASS` must never be reported as `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED`.
