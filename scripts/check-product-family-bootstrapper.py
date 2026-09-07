#!/usr/bin/env python3
from __future__ import annotations
import json, pathlib, sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools" / "QS3D.ProductBootstrapper"
MANIFEST = ROOT / "installer" / "product-family.manifest.json"
errors: list[str] = []

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
        for generation in ("2021", "2022", "2023", "2024", "2025", "2026", "2027"):
            component = by_key.get(("autocad", generation))
            if not component: errors.append(f"missing AutoCAD {generation} manifest component")
            elif component.get("enabled") is not False: errors.append(f"AutoCAD {generation} must remain pending/disabled until durable integrated release")
        for generation in ("V25", "V26"):
            component = by_key.get(("bricscad", generation))
            if not component: errors.append(f"missing BricsCAD {generation} manifest component")
            elif component.get("enabled") is not False: errors.append(f"BricsCAD {generation} initial family component must remain disabled")
except Exception as exc:
    errors.append(f"could not parse family manifest: {exc}")

if errors:
    print("QS3D product-family bootstrapper guard FAILED", file=sys.stderr)
    for error in errors: print(f"- {error}", file=sys.stderr)
    raise SystemExit(1)
print("QS3D product-family bootstrapper guard PASS")
