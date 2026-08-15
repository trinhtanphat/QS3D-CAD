# CI and integration policy

This file is the repository-level source of truth for CI ownership after multi-agent work.

## Per-agent task CI

`.github/workflows/ci.yml` is the canonical automatic task-validation workflow. It runs for implementation-relevant changes on `agent/**`, `recovery/**`, `integration/**`, pull requests targeting `main`, pushes to `main`, and manual dispatch.

Multiple agents share the workflow definition but every required run validates its own exact branch/PR head SHA. Ten independent implementation task branches therefore have ten independent CI results.

A GitHub Issue is coordination only; it has no source tree to build. CI evidence belongs to the branch/PR SHA referenced by the Issue when CI is required.

## CI-neutral-only exemption

The full build/test CI is skipped when **all** changed files are limited to documentation or non-executable housekeeping paths configured in `.github/workflows/ci.yml`: `docs/**`, `**/*.md`, `.gitignore`, `.gitattributes`, `.editorconfig`, `LICENSE*`, `NOTICE*`, and GitHub Issue/PR templates.

Docs/Markdown/housekeeping-only tasks may complete without an artificial build CI run after relevant lightweight validation. This exemption is path-based, not commit-message-based. A `chore:` commit still requires normal CI if it changes source, tests, project/build files, dependencies, scripts, workflows, packaging, installer/runtime-affecting configuration, submodule pins, or any other non-ignored path. Mixed changes always run CI.

`.github/workflows/**` is intentionally not ignored, so CI changes test themselves.

## Mandatory completion gate

For any task that is not CI-neutral-only, an implementation agent must not report a task completed or stop as completed until the required CI run for the **exact current branch/PR head SHA** has conclusion `success`.

A green run for an older SHA, another branch, another PR, or `main` does not count. Any new implementation-relevant task commit invalidates earlier green evidence for completion.

If CI fails, keep the lane active, fix the real defect on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`, push a new SHA and repeat. Never weaken architecture, vendor isolation, persistence, installer, security or test guards merely to obtain green status.

Native DWG/CAD runtime evidence that cannot be proven by repository-safe CI remains an explicit environment gate; remote CI success must never be relabeled as native PASS.

## Final-tree rule

Canonical progression for implementation-relevant changes:

```text
CLAIM_VISIBLE
  -> AGENT_BRANCH
  -> EXACT-HEAD CI
  -> CI_GREEN
  -> PR_READY
  -> INTEGRATION_BRANCH
  -> EXACT-INTEGRATION CI
  -> CI_GREEN
  -> ONE_AUTHORIZED_MERGE_TO_MAIN
  -> EXACT-MAIN CI
  -> CI_GREEN
  -> ALL_DONE
```

CI-neutral-only work uses the same branch/PR authorization path without manufacturing a build-CI requirement. CI success is a completion/quality gate, not direct-main authorization.

## Integration

For a multi-agent batch, combine participating implementation work on `integration/<batch-id>`, require green CI for the exact integration head when implementation-relevant paths changed, then perform the reviewed authorized landing. Require green CI again for the exact resulting `main` SHA when applicable before reporting `ALL MERGED TO MAIN`.

## Release boundary

Release/bootstrap workflows are not substitutes for per-agent task CI. Keep publication/tagging/installer-release semantics separate from ordinary branch/PR validation. An implementation task agent should use `.github/workflows/ci.yml` as its mandatory completion evidence unless a task explicitly requires an additional release/native gate.

## Evidence boundaries

- In-memory/reference CI is not native DWG/CAD runtime proof.
- Native backend qualification, installer execution, signing and licensed-host evidence remain separate when required.
- Keep the `QS3D-Platform` submodule pinned to the exact reviewed dependency SHA used by the candidate.

## GitHub protection

Repository settings should require the stable `QS3D CAD CI / validate` status for implementation PRs to `main`, require the intended PR/integration path, block force-push and branch deletion, and keep bypass narrow. If a future ruleset requires a status on docs-only PRs, use a lightweight status/ruleset condition instead of forcing full build CI for CI-neutral changes.
