#!/usr/bin/env python3
from __future__ import annotations
import json, pathlib, sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools" / "QS3D.ProductBootstrapper"
MANIFEST = ROOT / "installer" / "product-family.manifest.json"
ENGINEERING_MANIFEST = ROOT / "installer" / "product-family.engineering.manifest.json"
PACKAGE_SCRIPT = ROOT / "scripts" / "package-family-bootstrapper.ps1"
FAMILY_RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "release-family-windows.yml"
AUTOCAD_HANDOFF_SCRIPT = ROOT / "scripts" / "invoke-autocad-native-qualification-handoff.ps1"
errors: list[str] = []

AUTOCAD_ENGINEERING = {
    "repository": "trinhtanphat/QS3D-AutoCAD",
    "releaseTag": "test-v0.1.0-ci.266",
    "sourceSha": "8dd65a7e5061430f76e027467261beb8f18c69a8",
    "assetName": "QS3D-AutoCAD-0.0.0-ci-Setup.exe",
    "downloadUrl": "https://github.com/trinhtanphat/QS3D-AutoCAD/releases/download/test-v0.1.0-ci.266/QS3D-AutoCAD-0.0.0-ci-Setup.exe",
    "sha256": "9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de",
    "bytes": 67998359,
    "packageKind": "exe",
    "installStrategy": "execute",
}
AUTOCAD_GENERATIONS = ("2021", "2022", "2023", "2024", "2025", "2026", "2027")

required = [
    TOOL / "QS3D.ProductBootstrapper.csproj",
    TOOL / "ProductFamilyManifestLoader.cs",
    TOOL / "ProductFamilyManifestValidator.cs",
    TOOL / "HostDiscovery.cs",
    TOOL / "InstallPlanning.cs",
    TOOL / "PackageAcquisition.cs",
    TOOL / "PackageInstallation.cs",
    TOOL / "BootstrapperCoordinator.cs",
    MANIFEST,
    ENGINEERING_MANIFEST,
    PACKAGE_SCRIPT,
    FAMILY_RELEASE_WORKFLOW,
    AUTOCAD_HANDOFF_SCRIPT,
    ROOT / "docs" / "PRODUCT-FAMILY-INSTALLER.md",
]
for path in required:
    if not path.is_file(): errors.append(f"missing {path.relative_to(ROOT)}")

if (TOOL / "QS3D.ProductBootstrapper.csproj").is_file():
    project = (TOOL / "QS3D.ProductBootstrapper.csproj").read_text(encoding="utf-8", errors="replace")
    for token in ("AcMgd", "AcDbMgd", "AcCoreMgd", "BrxMgd", "TD_Mgd"):
        if token in project: errors.append(f"bootstrapper project references vendor managed assembly token {token}")

for project in (ROOT / "src").glob("**/*.csproj"):
    text = project.read_text(encoding="utf-8", errors="replace")
    if "QS3D.ProductBootstrapper" in text:
        errors.append(f"standalone runtime project depends on product bootstrapper: {project.relative_to(ROOT)}")

try:
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1: errors.append("family manifest schemaVersion must equal 1")
    components = data.get("components")
    if not isinstance(components, list) or len(components) > 32: errors.append("family manifest components must be a list of at most 32")
    else:
        by_key = {(c.get("product"), c.get("hostGeneration")): c for c in components if isinstance(c, dict)}
        for component in (c for c in components if isinstance(c, dict)):
            release_tag = str(component.get("releaseTag") or "")
            qualification = str(component.get("qualification") or "").lower()
            if release_tag.startswith("test-v"):
                errors.append(f"production manifest component {component.get('id')} must not reference engineering test release {release_tag}")
            if component.get("enabled") is True and "engineering" in qualification:
                errors.append(f"production-enabled component {component.get('id')} must not carry engineering qualification")
            if component.get("enabled") is True and component.get("product") == "autocad" and release_tag.startswith("test-v"):
                errors.append(f"production-enabled AutoCAD component {component.get('id')} must not reference engineering test release")
            if (
                component.get("enabled") is True
                and component.get("product") == "bricscad"
                and component.get("packageKind") == "zip"
                and not str(component.get("destination") or "").strip()
            ):
                errors.append(f"production-enabled BricsCAD ZIP component {component.get('id')} requires explicit destination")
        for generation in AUTOCAD_GENERATIONS:
            component = by_key.get(("autocad", generation))
            if not component: errors.append(f"missing AutoCAD {generation} manifest component")
            elif component.get("enabled") is not False: errors.append(f"AutoCAD {generation} must remain pending/disabled until durable integrated release")
        for generation in ("V25", "V26"):
            component = by_key.get(("bricscad", generation))
            if not component: errors.append(f"missing BricsCAD {generation} manifest component")
            elif component.get("enabled") is not False: errors.append(f"BricsCAD {generation} initial family component must remain disabled")
except Exception as exc:
    errors.append(f"could not parse family manifest: {exc}")

if ENGINEERING_MANIFEST.is_file():
    try:
        engineering = json.loads(ENGINEERING_MANIFEST.read_text(encoding="utf-8"))
        if engineering.get("schemaVersion") != 1: errors.append("engineering family manifest schemaVersion must equal 1")
        components = engineering.get("components")
        if not isinstance(components, list):
            errors.append("engineering family manifest components must be a list")
        else:
            by_key = {(c.get("product"), c.get("hostGeneration")): c for c in components if isinstance(c, dict)}
            if len(components) != len(AUTOCAD_GENERATIONS):
                errors.append("engineering family manifest must contain exactly seven AutoCAD generations")
            for generation in AUTOCAD_GENERATIONS:
                component = by_key.get(("autocad", generation))
                if not component:
                    errors.append(f"engineering manifest missing AutoCAD {generation}")
                    continue
                if component.get("enabled") is not True:
                    errors.append(f"engineering AutoCAD {generation} component must be enabled for native-test planning")
                qualification = str(component.get("qualification") or "").lower()
                if "engineering" not in qualification or "not-native-pass" not in qualification:
                    errors.append(f"engineering AutoCAD {generation} must explicitly remain engineering/not-native-pass")
                for key, expected in AUTOCAD_ENGINEERING.items():
                    if component.get(key) != expected:
                        errors.append(f"engineering AutoCAD {generation} {key} drifted from exact CI #266 candidate")
    except Exception as exc:
        errors.append(f"could not parse engineering family manifest: {exc}")

if PACKAGE_SCRIPT.is_file():
    package_text = PACKAGE_SCRIPT.read_text(encoding="utf-8", errors="replace")
    if "product-family.engineering.manifest.json" not in package_text:
        errors.append("family bootstrapper package must ship the engineering qualification manifest")

if FAMILY_RELEASE_WORKFLOW.is_file():
    workflow_text = FAMILY_RELEASE_WORKFLOW.read_text(encoding="utf-8", errors="replace")
    workflow_tokens = (
        "workflow_dispatch",
        "confirm_release",
        "source_sha",
        "push:",
        "branches:",
        "family-release/**",
        "github.event_name",
        "github.sha",
        "RELEASE",
        "family-v",
        "git merge-base --is-ancestor",
        "origin/main",
        "scripts/package-family-bootstrapper.ps1",
        "QS3D-Family-Setup-win-x64.exe",
        "QS3D-Family-Setup-win-x64.exe.sha256",
        "product-family.manifest.json",
        "product-family.manifest.json.sha256",
        "product-family.engineering.manifest.json",
        "product-family.engineering.manifest.json.sha256",
        "gh release view",
        "gh release upload",
        "gh release create",
    )
    for token in workflow_tokens:
        if token not in workflow_text:
            errors.append(f"family release workflow missing required token: {token}")
    if "push:" in workflow_text and "tags:" in workflow_text:
        errors.append("family release workflow must not use tag-triggered publication in this lane")

if errors:
    print("QS3D product-family bootstrapper guard FAILED", file=sys.stderr)
    for error in errors: print(f"- {error}", file=sys.stderr)
    raise SystemExit(1)
print("QS3D product-family bootstrapper guard PASS")
