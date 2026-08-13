# Agent policy

1. `QS3D-CAD` is standalone; never add a runtime dependency on BricsCAD.
2. `QS3D-Platform` is the host-neutral source of contracts/domain behavior. Keep the submodule pinned to an exact reviewed commit.
3. Never commit proprietary CAD SDK binaries, license files, credentials or private/customer drawings.
4. Third-party SDK types stay inside dedicated adapter projects and must not leak into public QS3D APIs.
5. In-memory/bootstrap tests are not DWG/native-runtime evidence.
6. Add deterministic regression coverage for behavioral changes and round-trip/runtime evidence when a native adapter exists.
7. Do not force-push over concurrent work. Refresh exact `main` before write and prefer coherent request-scoped commits.
8. Local agents are restricted to work that genuinely requires licensed/native CAD/SDK/device/runtime access. They must not take repo-only CI/source-fix lanes from remote agents. Follow `docs/LOCAL-NATIVE-QUALIFICATION.md`.
9. `REFERENCE_PASS` must never be promoted to `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` without exact product-SHA/backend-version evidence.
10. `PLANNING.md` is the architecture baseline; standalone ownership, vendor isolation and DWG-fidelity gates require explicit documentation to change.
