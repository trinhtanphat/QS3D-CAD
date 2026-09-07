# QS3D Product-Family Release Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a durable QS3D Family Windows preview from an exact `QS3D-CAD/main` SHA while preserving fail-closed AutoCAD/BricsCAD production gates and making the pinned AutoCAD CI #266 candidate easy to qualify on real hosts.

**Architecture:** Keep the existing family bootstrapper and manifests as the distribution core. Add a dedicated family-release workflow, extend the existing family guard to validate that workflow and production/native boundaries, add a read-only AutoCAD native-qualification handoff script, and publish exactly six family assets under deterministic tag `family-v$VERSION`. Vendor native evidence and signing remain owned by vendor repositories and are never synthesized by hosted CI.

**Tech Stack:** GitHub Actions YAML, PowerShell 7, Python 3 source guards, .NET 8 bootstrapper, GitHub Releases via `gh` CLI.

**Spec:** `docs/superpowers/specs/2026-09-07-family-release-completion-design.md`

## Global Constraints

- Implementation stays off `main` until reviewed final integration landing.
- Family release tag is exactly `family-v$VERSION`; no free-form family tag input.
- Family release contains exactly six assets: bootstrapper EXE, production manifest, engineering manifest, and one `.sha256` sidecar for each.
- Existing standalone `release-windows.yml` behavior remains independent.
- Production AutoCAD 2021-2027 entries remain disabled until native acceptance + signed durable AutoCAD release exist.
- AutoCAD 2026 retains CLR-major 8-or-10 native qualification semantics.
- Production BricsCAD V25/V26 remain disabled until exact native/release/install-contract evidence exists.
- Engineering AutoCAD manifest remains pinned to `test-v0.1.0-ci.266` / source `8dd65a7e5061430f76e027467261beb8f18c69a8` / Setup SHA-256 `9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de` / bytes `67998359`.
- Hosted CI must never write native PASS evidence or mutate vendor repository acceptance variables.

---

### Task 1: Add RED family-release and manifest-boundary guards

**Files:**
- Modify: `scripts/check-product-family-bootstrapper.py`
- Modify: `scripts/validate-family-bootstrapper.ps1`

**Interfaces:**
- Consumes: existing production/engineering manifests and `scripts/package-family-bootstrapper.ps1`.
- Produces: deterministic source guard requirements for `.github/workflows/release-family-windows.yml` and `scripts/invoke-autocad-native-qualification-handoff.ps1`.

- [ ] **Step 1: Extend the Python guard with missing-file RED assertions**

Add constants:

```python
FAMILY_RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "release-family-windows.yml"
AUTOCAD_HANDOFF_SCRIPT = ROOT / "scripts" / "invoke-autocad-native-qualification-handoff.ps1"
```

Add both paths to `required`. Add workflow-token checks requiring these exact semantic tokens when the workflow exists:

```python
workflow_tokens = (
    "workflow_dispatch",
    "confirm_release",
    "source_sha",
    "family-v",
    "scripts/package-family-bootstrapper.ps1",
    "QS3D-Family-Setup-win-x64.exe",
    "product-family.manifest.json",
    "product-family.engineering.manifest.json",
    "gh release",
)
```

Add production boundary logic:

```python
if component.get("enabled") is True and component.get("product") == "autocad" and str(component.get("releaseTag") or "").startswith("test-v"):
    errors.append(f"production-enabled AutoCAD component {component.get('id')} must not reference engineering test release")
if component.get("enabled") is True and component.get("product") == "bricscad" and component.get("packageKind") == "zip" and not str(component.get("destination") or "").strip():
    errors.append(f"production-enabled BricsCAD ZIP component {component.get('id')} requires explicit destination")
```

- [ ] **Step 2: Make family validation execute the handoff smoke after the boundary guard**

Append this checked invocation before the final PASS line:

```powershell
Write-Host '== AutoCAD native qualification handoff smoke =='
Invoke-CheckedNative 'AutoCAD native qualification handoff smoke' 'pwsh' @(
    '-NoProfile', '-File', 'scripts/invoke-autocad-native-qualification-handoff.ps1',
    '-HostGeneration', '2026', '-DryRunBootstrapper'
)
```

- [ ] **Step 3: Open a draft implementation PR on the RED head and run CI**

Expected family guard failure before implementation:

```text
missing .github/workflows/release-family-windows.yml
missing scripts/invoke-autocad-native-qualification-handoff.ps1
```

Existing authoritative validation must remain green before the family guard failure.

- [ ] **Step 4: Commit RED evidence**

Commit message:

```text
test(release): require family preview publication contract #53
```

---

### Task 2: Implement the family release workflow to GREEN

**Files:**
- Create: `.github/workflows/release-family-windows.yml`

**Interfaces:**
- Consumes: checked-in `VERSION`, `scripts/validate.ps1`, `scripts/validate-family-bootstrapper.ps1`, `scripts/package-family-bootstrapper.ps1`.
- Produces: manually dispatched family preview release `family-v$VERSION` with exact-source and six-asset verification.

- [ ] **Step 1: Create manual-dispatch inputs and exact-source checkout**

Use:

```yaml
name: QS3D Family Windows Release

on:
  workflow_dispatch:
    inputs:
      confirm_release:
        description: "Type RELEASE to publish the family preview from the selected source"
        required: true
        type: string
      source_sha:
        description: "Exact lowercase 40-character QS3D-CAD source SHA"
        required: true
        type: string

permissions:
  contents: write
```

Checkout `${{ inputs.source_sha }}` with recursive submodules and `fetch-depth: 0`.

- [ ] **Step 2: Validate release identity and main ancestry**

The PowerShell identity step must:

```powershell
if ($env:RELEASE_CONFIRMATION -ne 'RELEASE') { throw 'Manual family release requires confirm_release=RELEASE.' }
if ($env:RELEASE_SOURCE_INPUT -cnotmatch '^[0-9a-f]{40}$') { throw 'Manual family release requires exact lowercase 40-character source_sha.' }
$sourceSha = (git rev-parse HEAD).Trim().ToLowerInvariant()
if ($sourceSha -ne $env:RELEASE_SOURCE_INPUT) { throw "Checked-out SHA '$sourceSha' does not match requested source." }
$version = (Get-Content VERSION -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw "VERSION is not release-safe: '$version'." }
$tag = "family-v$version"
git fetch origin main --no-tags
if ($LASTEXITCODE -ne 0) { throw 'Unable to refresh origin/main.' }
git merge-base --is-ancestor $sourceSha origin/main
if ($LASTEXITCODE -ne 0) { throw "Family release source $sourceSha is not an ancestor of origin/main." }
```

Then inspect existing tag. If it exists, require it to resolve to the same exact source SHA. Export `version`, `tag`, `tag_exists`, and `source_sha` as step outputs.

- [ ] **Step 3: Validate and package family assets**

Run:

```powershell
./scripts/validate.ps1
./scripts/validate-family-bootstrapper.ps1
./scripts/package-family-bootstrapper.ps1 -SkipValidation
```

Then verify all six local paths exist and validate each sidecar by recomputing SHA-256.

- [ ] **Step 4: Publish or refresh only the exact same-source prerelease**

The six asset paths are exactly:

```text
artifacts/family-bootstrapper/QS3D-Family-Setup-win-x64.exe
artifacts/family-bootstrapper/QS3D-Family-Setup-win-x64.exe.sha256
artifacts/family-bootstrapper/product-family.manifest.json
artifacts/family-bootstrapper/product-family.manifest.json.sha256
artifacts/family-bootstrapper/product-family.engineering.manifest.json
artifacts/family-bootstrapper/product-family.engineering.manifest.json.sha256
```

For an existing release, require `isPrerelease=true` and same tag/source before `gh release upload ... --clobber`. For a new release, create with `--target $sourceSha --prerelease` and title `QS3D Family $tag`.

- [ ] **Step 5: Verify the published release**

Fetch:

```powershell
$json = gh release view $env:RELEASE_TAG --repo $env:GITHUB_REPOSITORY --json tagName,isPrerelease,assets | ConvertFrom-Json
```

Require:
- exact tag name;
- prerelease true;
- asset name set equals exactly the six required names;
- Git tag target equals exact source SHA after `git fetch --force --tags origin`.

- [ ] **Step 6: Commit GREEN workflow**

Commit message:

```text
feat(release): add exact-source family preview workflow #53
```

---

### Task 3: Implement read-only AutoCAD native qualification handoff

**Files:**
- Create: `scripts/invoke-autocad-native-qualification-handoff.ps1`
- Modify: `docs/PRODUCT-FAMILY-INSTALLER.md`

**Interfaces:**
- Consumes: `installer/product-family.engineering.manifest.json` and optionally a local `QS3D-AutoCAD` checkout path.
- Produces: validated operator instructions for one exact AutoCAD generation; never creates PASS evidence.

- [ ] **Step 1: Add script parameters and generation validation**

Use:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string]$HostGeneration,
    [string]$AutoCADRepositoryPath,
    [switch]$DryRunBootstrapper
)
```

- [ ] **Step 2: Validate exact CI #266 component metadata**

Load `installer/product-family.engineering.manifest.json`, select exactly one component where `product=autocad` and `hostGeneration=$HostGeneration`, and require:

```powershell
$expected = @{
  releaseTag = 'test-v0.1.0-ci.266'
  sourceSha = '8dd65a7e5061430f76e027467261beb8f18c69a8'
  assetName = 'QS3D-AutoCAD-0.0.0-ci-Setup.exe'
  sha256 = '9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de'
  bytes = 67998359
}
```

Require `enabled=true` and qualification text containing both `engineering` and `not-native-pass`.

- [ ] **Step 3: Optionally run family bootstrapper dry-run**

When `-DryRunBootstrapper` is set, prefer existing packaged EXE at `artifacts/family-bootstrapper/QS3D-Family-Setup-win-x64.exe`; if absent, run the bootstrapper project via:

```powershell
dotnet run --project tools/QS3D.ProductBootstrapper/QS3D.ProductBootstrapper.csproj -c Release -- --manifest installer/product-family.engineering.manifest.json --product autocad --host-generation $HostGeneration --dry-run
```

Fail if exit code is nonzero.

- [ ] **Step 4: Print authoritative AutoCAD evidence commands**

Print a command sequence using `new-native-acceptance.ps1`, `record-native-runtime.ps1`, repeated `record-native-result.ps1`, and `validate-native-acceptance.ps1`. For 2021-2024, final validation must explicitly use the requested legacy generation and must not claim sibling legacy generations. For 2025-2027, explain that formal production validation remains the default modern matrix 2025/2026/2027 after all three sessions exist.

When `-AutoCADRepositoryPath` is supplied, require these scripts to exist under that checkout:

```text
scripts/new-native-acceptance.ps1
scripts/record-native-runtime.ps1
scripts/record-native-result.ps1
scripts/validate-native-acceptance.ps1
```

- [ ] **Step 5: Document operator use**

Add examples:

```powershell
./scripts/invoke-autocad-native-qualification-handoff.ps1 -HostGeneration 2026 -DryRunBootstrapper
./scripts/invoke-autocad-native-qualification-handoff.ps1 -HostGeneration 2021 -AutoCADRepositoryPath C:\src\QS3D-AutoCAD
```

State explicitly that the script validates/plans only and cannot mark native checks PASS.

- [ ] **Step 6: Run all seven no-host smoke invocations**

Run the script for 2021, 2022, 2023, 2024, 2025, 2026, 2027 with `-DryRunBootstrapper`. All must exit 0 and identify the exact CI #266 candidate.

- [ ] **Step 7: Commit**

Commit message:

```text
feat(release): add AutoCAD native qualification handoff #53
```

---

### Task 4: Refresh manifest provenance and tracker documentation without enabling production

**Files:**
- Modify: `installer/product-family.manifest.json`
- Modify: `docs/PRODUCT-FAMILY-INSTALLER.md`
- Modify: AutoCAD issue bodies/comments for `QS3D-AutoCAD#52`, `#58`, `#60`

**Interfaces:**
- Consumes: final implementation commit/source lineage.
- Produces: truthful source-state metadata while preserving every vendor production component as disabled.

- [ ] **Step 1: Refresh production manifest provenance only**

Set production manifest `generatedFromSource` to the exact implementation source commit that performs the provenance refresh. Do not modify any AutoCAD/BricsCAD `enabled` field from `false` and do not add `test-v*` package metadata to production.

Because the exact commit SHA is only known after the implementation tree exists, perform this as a final single-file commit after all behavioral files are committed, using that commit's parent SHA as the documented source state if the strict self-reference cannot be represented without an impossible hash fixed point. Document the semantic precisely: `generatedFromSource` is the last source state whose family qualification data the manifest describes, not a self-hash of the manifest commit.

- [ ] **Step 2: Document BricsCAD enablement gates**

In `docs/PRODUCT-FAMILY-INSTALLER.md`, preserve V25/V26 disabled state and list the exact missing gates: native acceptance, durable public family-intended release asset, explicit ZIP destination/install contract, exact bytes/SHA-256.

- [ ] **Step 3: Update AutoCAD tracker source status**

For #52/#58/#60, ensure latest issue text/comment says:
- source merged through #85 at `8dd65a7e5061430f76e027467261beb8f18c69a8`;
- exact-main CI `34095670868` green;
- issue remains open only for native evidence;
- no stale `MERGED TO MAIN: NO` statement should be interpreted as current status.

Do not close these issues.

- [ ] **Step 4: Commit docs/manifest cleanup**

Commit message:

```text
docs(release): clarify family production and native gates #53
```

---

### Task 5: Final CI, integration, publication, and closeout

**Files:**
- Modify: claim file `docs/agent-work-claims/2026-09-07-chatgpt-gpt56sol-family-release-completion.md`
- Modify: issue #53 metadata/comments

**Interfaces:**
- Consumes: exact final implementation PR head and `main` SHA after merge.
- Produces: public family preview release, exact-main verification, and terminal claim.

- [ ] **Step 1: Run exact final PR-head CI**

Require:
- authoritative validation PASS;
- product-family boundary/build/smoke PASS;
- family release workflow source guard PASS;
- family package smoke PASS.

Freeze the exact PR head SHA after this run starts.

- [ ] **Step 2: Revalidate drift and reviews**

Compare current `main` to the PR base. Reject integration if relevant release/manifest surfaces drifted. Confirm no unresolved review thread or blocking review exists.

- [ ] **Step 3: Merge with expected head SHA**

Use merge method `merge` and the frozen expected PR-head SHA. Never force, bypass, or merge red CI.

- [ ] **Step 4: Require exact-main CI**

Observe CI on the new merge SHA and require full success before publication.

- [ ] **Step 5: Dispatch family release against exact main SHA**

Run `.github/workflows/release-family-windows.yml` with:

```text
confirm_release=RELEASE
source_sha=<exact current main SHA>
```

Expected tag with current version: `family-v0.1.0-preview.5`.

- [ ] **Step 6: Verify release metadata and six assets**

Require release tag target = exact main SHA, prerelease = true, and exact asset set:

```text
QS3D-Family-Setup-win-x64.exe
QS3D-Family-Setup-win-x64.exe.sha256
product-family.manifest.json
product-family.manifest.json.sha256
product-family.engineering.manifest.json
product-family.engineering.manifest.json.sha256
```

Record all exposed GitHub asset digests and sizes in issue #53.

- [ ] **Step 7: Close issue #53 and terminalize claim**

Issue #53 closeout must state exact merge SHA, exact-main CI run, family tag, release source SHA, and six assets. Set claim:

```text
Status: COMPLETED
MERGED TO MAIN: YES
EXACT-MAIN CI: GREEN
FAMILY PREVIEW RELEASE: PUBLISHED
PRODUCTION VENDOR NATIVE QUALIFICATION: PENDING EXTERNAL NATIVE EVIDENCE
```

Land the claim closeout as a docs-only PR and require one final exact-main CI on the closeout merge SHA.
