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
        ("confirm_release:", "manual release must require an explicit confirmation input"),
        ("RELEASE_CONFIRMATION", "manual release confirmation must be bound into the validation step"),
        ("-ne 'RELEASE'", "manual release must fail closed unless confirmation is RELEASE"),
        ("git tag --list", "release must probe optional version tags without terminating on absence"),
        ("git rev-list -n 1", "release must bind an existing version tag to the checked-out source SHA"),
        ("Refusing to replace assets across source SHAs", "release must refuse cross-SHA asset replacement"),
        ("tag_exists=", "release creation must distinguish verified existing tags from new tags"),
        ("contents: write", "release workflow needs contents write permission"),
        ("submodules: recursive", "release workflow must checkout the exact Platform gitlink"),
        ("./scripts/package-windows.ps1", "release workflow must use the authoritative Windows packager"),
        ("actions/upload-artifact@v4", "release workflow must retain installer artifacts"),
        ("'release', 'create'", "release workflow must create the GitHub Release when absent"),
        ("gh release upload", "release workflow must idempotently refresh same-SHA release assets"),
        ("QS3D-CAD-Setup-win-x64.exe.sha256", "release workflow must publish checksum evidence"),
    ):
        require(token in release, description, failures)
    require("branches:\n      - main" not in release, "release workflow must never auto-publish from a main-branch push", failures)

    bootstrap = read(".github/workflows/bootstrap-preview-tag.yml")
    for token, description in (
        ("RELEASE_REF: release/v0.1.0-preview.2", "preview bootstrap must use the dedicated validated release branch"),
        ("TARGET_SHA: 14b0d374769cb571bb5150654ea8f0e209ea658d", "preview bootstrap must bind the release branch to the validated CAD SHA"),
        ("git/ref/heads/$RELEASE_REF", "bootstrap must read back the release branch ref before dispatch"),
        ("actual\" != \"$TARGET_SHA", "bootstrap must fail closed if release branch source identity drifts"),
        ("gh workflow run release-windows.yml", "bootstrap must dispatch the hardened Windows release workflow"),
        ("--ref \"$RELEASE_REF\"", "release workflow must run from the exact validated release branch"),
    ):
        require(token in bootstrap, description, failures)

    if failures:
        print("QS3D CAD release contract FAILED")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"QS3D CAD release contract PASS ({version})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
