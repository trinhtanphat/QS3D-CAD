#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    target = ROOT / path
    if not target.is_file():
        raise RuntimeError(f"missing release contract file: {path}")
    return target.read_text(encoding="utf-8")


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def main() -> int:
    failures: list[str] = []

    version = read("VERSION").strip()
    require(
        re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version) is not None,
        f"VERSION is not release-safe: {version!r}",
        failures,
    )

    desktop = read("src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj")
    for token, description in (
        ("<OutputType>WinExe</OutputType>", "desktop project must publish a Windows executable"),
        ("<TargetFramework>net8.0-windows</TargetFramework>", "desktop project must target net8.0-windows"),
        ("<AssemblyName>QS3D.CAD</AssemblyName>", "desktop executable identity must remain QS3D.CAD"),
        ("<ApplicationIcon>Assets\\qs3d-app.ico</ApplicationIcon>", "desktop executable must keep the QS3D application icon"),
    ):
        require(token in desktop, description, failures)

    package = read("scripts/package-windows.ps1")
    for token, description in (
        ('[ValidateSet("win-x64")]', "packager must stay fail-closed to the supported Windows x64 runtime"),
        ('-r $Runtime', "packager must publish the selected supported Windows runtime"),
        ('--self-contained true', "installer payload must remain self-contained"),
        ('QS3D.CAD.exe', "packager must verify the desktop executable"),
        ('QS3D-CAD-Setup-win-x64.exe', "packager must verify the installer output"),
        ('Get-FileHash -Algorithm SHA256', "packager must emit SHA-256 evidence"),
    ):
        require(token in package, description, failures)

    installer = read("installer/QS3D-CAD.iss")
    for token, description in (
        ("AppName=QS3D CAD", "installer product name changed unexpectedly"),
        ("OutputBaseFilename=QS3D-CAD-Setup-win-x64", "installer filename contract changed unexpectedly"),
        ("ArchitecturesInstallIn64BitMode=x64compatible", "installer must use the 64-bit installation lane"),
        ("SetupIconFile=..\\src\\QS3D.Cad.Desktop\\Assets\\qs3d-app.ico", "installer must use the QS3D application icon"),
        ("UninstallDisplayIcon={app}\\QS3D.CAD.exe", "installer uninstall identity must reference QS3D.CAD.exe"),
        ("VersionInfoVersion={#FileVersion}", "installer must keep numeric PE file version separate from preview SemVer"),
        ("VersionInfoProductVersion={#FileVersion}", "installer product-version metadata must stay numeric for Inno Setup"),
    ):
        require(token in installer, description, failures)
    require("VersionInfoProductVersion={#AppVersion}" not in installer, "preview SemVer must not be written into numeric installer product-version metadata", failures)

    ci = read(".github/workflows/ci.yml")
    for token, description in (
        ("submodules: recursive", "CI must checkout the exact Platform gitlink recursively"),
        ("./scripts/validate.ps1", "CI must run authoritative standalone validation"),
        ("Install Inno Setup 6", "CI must provision the actual Windows installer compiler"),
        ("./scripts/package-windows.ps1 -SkipValidation", "CI must compile the real installer after authoritative validation"),
        ("QS3D-CAD-Setup-win-x64.exe.sha256", "CI must verify installer checksum evidence"),
    ):
        require(token in ci, description, failures)

    release = read(".github/workflows/release-windows.yml")
    for token, description in (
        ("source_sha:", "manual release must declare an exact source SHA input"),
        ("ref: ${{ inputs.source_sha || github.ref }}", "release checkout must bind directly to the selected source commit"),
        ("git rev-parse HEAD", "release must derive source identity from the actual checkout"),
        ("RELEASE_SOURCE_INPUT", "manual release must compare requested source SHA with checkout"),
        ("source_sha=$sourceSha", "release must export exact checked-out source identity"),
        ("git tag --list", "release must probe optional version tags without terminating on absence"),
        ("Refusing to replace assets across source SHAs", "release must refuse cross-SHA asset replacement"),
        ("'--target', $env:RELEASE_SOURCE_SHA", "new release/tag must target the exact validated source SHA"),
        ("git rev-list -n 1 $env:RELEASE_TAG", "release verification must resolve published tag to a commit"),
        ("contents: write", "release workflow needs contents write permission"),
        ("./scripts/package-windows.ps1", "release workflow must use the authoritative Windows packager"),
        ("actions/upload-artifact@v4", "release workflow must retain installer artifacts"),
        ("QS3D-CAD-Setup-win-x64.exe.sha256", "release workflow must publish checksum evidence"),
    ):
        require(token in release, description, failures)
    require("branches:\n      - main" not in release, "release workflow must never auto-publish from a main-branch push", failures)

    verifier = read(".github/workflows/bootstrap-preview-tag.yml")
    for token, description in (
        ("workflow_dispatch:", "published preview verifier must be manual-only"),
        ("RELEASE_TAG: v0.1.0-preview.2", "published preview verifier must bind the released tag"),
        ("TARGET_SHA: a3c0e6d098f02c8cfbb594020b20930491339d59", "published preview verifier must bind the released source SHA"),
        ("git/ref/tags/$RELEASE_TAG", "published preview verifier must read back tag source identity"),
        ("gh release view", "published preview verifier must inspect release metadata"),
        ("gh release download", "published preview verifier must download published assets for checksum verification"),
        ("5f6912569bbc43bbcfb7bdd18c902c35457aa91964c5d3b6f01264174d5c562e", "published preview verifier must lock the released installer digest"),
    ):
        require(token in verifier, description, failures)
    require("gh workflow run" not in verifier, "published preview verifier must not trigger another release", failures)
    require("actions: write" not in verifier, "published preview verifier must remain read-only", failures)

    if failures:
        print("QS3D CAD release contract FAILED")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"QS3D CAD release contract PASS ({version})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
