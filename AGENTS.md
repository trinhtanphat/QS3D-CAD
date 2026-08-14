# Agent policy

## Mandatory AI/chat-session lifecycle

Before substantive work, every AI agent/chat session must read `docs/AI-SESSION-WORKFLOW.md`, `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md` and follow them as mandatory owner policy.

1. Register/claim the lane first through a visible GitHub issue or claim PR; do not create a coordination-only direct `main` commit.
2. Publish a concrete plan before implementation.
3. Work on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`; ordinary agents/sessions do not push or merge implementation to `main`.
4. `fix bug`, `update code`, `commit push git`, `continue all`, `implement all`, `run CI`, `fix CI`, `loop until success` and equivalent prompts never grant `main` authority.
5. Only explicit owner integration authorization may change `main`, such as `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.
6. Run/observe applicable task-scoped CI on the branch/PR and continue diagnose -> fix -> push -> fresh CI until all required/applicable lane checks are green. CI ownership is not `main` authority and is not release/publish authority.
7. Every session must end with explicit `PROMPT/LANE STATUS: 100% COMPLETE/NOT 100% COMPLETE`, `SESSION CAN BE CLOSED/DELETED: YES/NO`, and separate `MERGED TO MAIN: YES/NO` plus exact SHA/evidence/blockers.
8. If not 100% complete and actionable work remains within the session's scope/tools/permissions, continue the loop instead of stopping at a checkpoint.

## Mandatory multi-agent integration

Before substantive repository work, refresh the latest `origin/main`, inspect existing `ACTIVE` / `BLOCKED` claim files plus open claim issues/PRs, and avoid overlapping reserved surfaces.

1. AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
2. Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`, then the normal integration path.
3. New claims are issue/PR-first under `docs/AGENT-WORK-REGISTRATION.md`; existing active claim files remain valid coordination history.
4. Implement only the reserved lane on a dedicated `agent/<agent>/<scope>` branch.
5. For a multi-agent batch, an explicitly authorized coordinator combines participating work on `integration/<batch-id>`, resolves semantic conflicts deliberately, runs combined validation, and performs one final authorized PR/landing to `main`.
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
