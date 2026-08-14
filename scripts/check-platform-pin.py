#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SUBMODULE = ROOT / "external" / "QS3D-Platform"
HOST_PROJECT = ROOT / "src" / "QS3D.Cad.Host" / "QS3D.Cad.Host.csproj"
SMOKE_PROJECT = ROOT / "tests" / "QS3D.Cad.SmokeTests" / "QS3D.Cad.SmokeTests.csproj"
LEGACY_ADVANCED_SHIM = ROOT / "src" / "QS3D.Cad.Host" / "PlatformAdvancedServicesCompat.cs"
LEGACY_BUILD_TARGETS = ROOT / "Directory.Build.targets"


def run(*args: str) -> str:
    process = subprocess.run(
        args,
        cwd=ROOT,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if process.returncode != 0:
        detail = process.stderr.strip() or process.stdout.strip()
        raise RuntimeError(f"{' '.join(args)} failed: {detail}")
    return process.stdout.strip()


def main() -> int:
    failures: list[str] = []

    try:
        index = run("git", "ls-files", "-s", "--", "external/QS3D-Platform")
    except RuntimeError as exc:
        print(f"FAILED: {exc}")
        return 1

    fields = index.split()
    if len(fields) < 4 or fields[0] != "160000":
        failures.append("external/QS3D-Platform must be a Git submodule entry with mode 160000")
        expected_sha = None
    else:
        expected_sha = fields[1]

    if not SUBMODULE.exists() or not (SUBMODULE / ".git").exists():
        failures.append("Platform submodule is not initialized; run: git submodule update --init --recursive")
        actual_sha = None
    else:
        process = subprocess.run(
            ["git", "-C", str(SUBMODULE), "rev-parse", "HEAD"],
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        actual_sha = process.stdout.strip() if process.returncode == 0 else None
        if actual_sha is None:
            failures.append("could not resolve checked-out Platform submodule HEAD")

    if expected_sha and actual_sha and expected_sha != actual_sha:
        failures.append(f"Platform checkout mismatch: gitlink={expected_sha}, checkout={actual_sha}")

    if not HOST_PROJECT.exists():
        failures.append("standalone Host project is missing")
    else:
        project_text = HOST_PROJECT.read_text(encoding="utf-8")
        required_projects = (
            "QS3D.Platform.Application",
            "QS3D.Platform.Cad.Abstractions",
            "QS3D.Platform.Diagnostics",
            "QS3D.Platform.Domain",
            "QS3D.Platform.Geometry",
            "QS3D.Platform.InMemory",
            "QS3D.Platform.Persistence",
            "QS3D.Platform.Quantity",
        )
        for project in required_projects:
            if project not in project_text:
                failures.append(f"Host project is missing shared reference {project}")

    if not SMOKE_PROJECT.exists():
        failures.append("standalone smoke project is missing")
    else:
        smoke_text = SMOKE_PROJECT.read_text(encoding="utf-8")
        for project in ("QS3D.Platform.Parity", "QS3D.Platform.Families"):
            if project not in smoke_text:
                failures.append(f"Smoke project is missing shared reference {project}")

    if SUBMODULE.exists():
        required_surfaces = (
            pathlib.Path("src/QS3D.Platform.Cad.Abstractions/CadAdvancedContracts.cs"),
            pathlib.Path("src/QS3D.Platform.Cad.Abstractions/CadConformance.cs"),
            pathlib.Path("src/QS3D.Platform.InMemory/InMemoryAdvancedServices.cs"),
            pathlib.Path("src/QS3D.Platform.Persistence/QS3D.Platform.Persistence.csproj"),
            pathlib.Path("src/QS3D.Platform.Persistence/ProjectContainerManifest.cs"),
            pathlib.Path("src/QS3D.Platform.Quantity/QS3D.Platform.Quantity.csproj"),
            pathlib.Path("src/QS3D.Platform.Quantity/QuantityScheduleCsv.cs"),
            pathlib.Path("src/QS3D.Platform.Parity/QS3D.Platform.Parity.csproj"),
            pathlib.Path("src/QS3D.Platform.Families/QS3D.Platform.Families.csproj"),
            pathlib.Path("scripts/check-netstandard20-boundary.py"),
            pathlib.Path("scripts/check-reference-services.py"),
            pathlib.Path("scripts/check-parity.py"),
            pathlib.Path("scripts/check-families.py"),
        )
        for relative in required_surfaces:
            if not (SUBMODULE / relative).exists():
                failures.append(f"pinned Platform is missing required surface {relative.as_posix()}")

    if LEGACY_ADVANCED_SHIM.exists():
        failures.append("legacy PlatformAdvancedServicesCompat.cs must not coexist with the authoritative pinned Platform implementation")
    if LEGACY_BUILD_TARGETS.exists():
        failures.append("legacy Directory.Build.targets compatibility exclusion is no longer allowed")

    if failures:
        print("Platform pin guard FAILED")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"Platform pin guard PASS ({expected_sha})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
