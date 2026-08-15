# Agent policy

## Mandatory multi-agent integration

Before substantive repository work, read `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md`, refresh the latest `origin/main`, and inspect every `ACTIVE` / `BLOCKED` claim under `docs/agent-work-claims/`.

1. AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
2. Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`.
3. Publish a visible claim before implementation and implement only the reserved lane on `agent/<agent>/<scope>`.
4. Push the final intended task head and open/update its PR before claiming remote completion.
5. `.github/workflows/ci.yml` runs automatically for implementation-relevant changes on `agent/**`, `recovery/**`, `integration/**`, PRs to `main`, and `main`. Docs/Markdown/non-executable housekeeping-only changes listed in `CI_POLICY.md` are intentionally excluded.
6. For any task that is not CI-neutral-only, an agent **must not report a task completed or stop as completed until CI is `success` for the exact current branch/PR head SHA**. Old green runs, another branch, another PR or `main` do not count.
7. A CI-neutral-only task may complete without full build CI only when every changed path belongs to the documented ignore set. `chore:` is not an exemption by itself; mixed/source/build/script/workflow/dependency/submodule changes still require CI.
8. If required CI fails, keep the task active, fix the real defect, push a new SHA and repeat the exact-SHA CI gate. Do not weaken architecture/tests/installer checks merely to get green.
9. For a multi-agent batch, combine participating implementation work on `integration/<batch-id>`, require green CI for the exact integration head when implementation-relevant paths changed, resolve semantic conflicts deliberately, and perform one authorized final PR/landing to `main`.
10. Require green CI again for the exact resulting `main` SHA when implementation-relevant paths changed before reporting `ALL MERGED TO MAIN`.
11. Never force-push `main`, reset it backwards, or overwrite concurrent work.

A GitHub Issue is a reservation/coordination surface, not a build target; when CI is required it must reference the branch/PR SHA whose CI proves the task. CI success is a quality/completion gate, not merge authorization. `CI_POLICY.md` is authoritative for the CI-neutral path exemption.

## QS3D-CAD product rules

1. `QS3D-CAD` is standalone; never add a runtime dependency on BricsCAD.
2. `QS3D-Platform` is the host-neutral source of contracts/domain behavior. Keep the submodule pinned to an exact reviewed commit.
3. Never commit proprietary CAD SDK binaries, license files, credentials or private/customer drawings.
4. Third-party SDK types stay inside dedicated adapter projects and must not leak into public QS3D APIs.
5. In-memory/bootstrap CI is not DWG/native-runtime evidence.
6. Add deterministic regression coverage for behavioral changes and round-trip/runtime evidence when a native adapter exists.
7. Refresh exact `main` before writes and prefer coherent request-scoped commits.
8. Local agents are restricted to work that genuinely requires licensed/native CAD/SDK/device/runtime access. Follow `docs/LOCAL-NATIVE-QUALIFICATION.md`.
9. `REFERENCE_PASS` must never be promoted to `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` without exact product-SHA/backend-version evidence.
10. `PLANNING.md` is the architecture baseline; standalone ownership, vendor isolation and DWG-fidelity gates require explicit documentation to change.
