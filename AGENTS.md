# Agent policy

## Mandatory multi-agent integration

Before substantive repository work, read `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md`, refresh the latest `origin/main`, and inspect every `ACTIVE` / `BLOCKED` claim under `docs/agent-work-claims/`.

1. AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
2. Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`, then the normal integration path.
3. Publish a visible claim before implementation. Prefer a tiny `claim/<agent>/<scope>` PR to `main`; a claim-only Markdown landing is coordination, not implementation.
4. Implement only the reserved lane on a dedicated `agent/<agent>/<scope>` branch.
5. For a multi-agent batch, combine participating work on `integration/<batch-id>`, resolve semantic conflicts deliberately, run combined validation, and perform one final PR/landing to `main`.
6. Never force-push `main`, reset it backwards, or overwrite concurrent work. Refresh immediately before integration and verify the resulting commit/tree is reachable from current `main`.
7. A branch, issue, PR, or old green CI run is not proof that all required work is merged. See `docs/AGENT-WORK-REGISTRATION.md` for the `ALL MERGED TO MAIN` gate.

## QS3D-CAD product rules

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
