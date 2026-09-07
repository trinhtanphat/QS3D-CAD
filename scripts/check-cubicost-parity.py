#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMAND = ROOT / "src/QS3D.Cad.Host/CubicostParityCommands.cs"
SEMANTIC = ROOT / "src/QS3D.Cad.Host/SemanticCommands.cs"
PROJECT = ROOT / "src/QS3D.Cad.Host/QS3D.Cad.Host.csproj"
SMOKE = ROOT / "tests/QS3D.Cad.SmokeTests/CubicostParityModuleSmoke.cs"
DOC = ROOT / "docs/CUBICOST-PARITY-STANDALONE.md"
EXPECTED_PLATFORM = "e029d4ba0de6ffe80575f7aed96affa1db1b9b33"


def main() -> int:
    failures: list[str] = []
    files = {"command": COMMAND, "semantic": SEMANTIC, "project": PROJECT, "smoke": SMOKE, "doc": DOC}
    for label, path in files.items():
        if not path.exists():
            failures.append(f"missing {label}: {path.relative_to(ROOT)}")
    if failures:
        return report(failures)

    texts = {label: path.read_text(encoding="utf-8") for label, path in files.items()}
    required = {
        "command": (
            'Name => "QSMEPRECOGNIZE"',
            'Name => "QSMEPTAKEOFF"',
            'Name => "QSMEPCLASH"',
            'Name => "QSMEPCLASHLOCATE"',
            'Name => "QSMEPISSUES"',
            "MepRecognitionProfiles.CreateDefault()",
            "new MepQuantityService().Aggregate",
            "new ClashDetectionService().Detect",
            "new CoordinationIssue(",
            "metersPerUnit",
            "QS3D.Mep.Length",
            "QS3D.Mep.Area",
            "QS3D.Mep.Volume",
            "context.Document.Editor.Selection.Set(new[] { left, right })",
            "CadTransactionMode.ReadOnly",
        ),
        "semantic": ("CubicostParityCommands.RegisterAll(registry)",),
        "project": ("QS3D.Platform.Parity",),
        "smoke": (
            "QSMEPRECOGNIZE",
            "QSMEPTAKEOFF 1",
            "QSMEPCLASH 0 1",
            "QSMEPCLASHLOCATE 1 0 1",
            "QSMEPISSUES 0 1",
        ),
        "doc": ("QS3D-Platform", EXPECTED_PLATFORM, "PENDING_NATIVE"),
    }
    for label, needles in required.items():
        for needle in needles:
            if needle not in texts[label]:
                failures.append(f"{label}: missing required token {needle!r}")

    for forbidden in (
        "BeginTransaction(CadTransactionMode.ReadWrite)",
        ".Append(",
        ".Update(",
        ".Erase(",
        "ModifiesDrawing",
        "Autodesk.",
        "Bricscad.",
        "Teigha.",
    ):
        if forbidden in texts["command"]:
            failures.append(f"command: forbidden mutation/vendor token {forbidden!r}")

    process = subprocess.run(
        ["git", "ls-files", "-s", "--", "external/QS3D-Platform"],
        cwd=ROOT,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    fields = process.stdout.split()
    if process.returncode != 0 or len(fields) < 4 or fields[0] != "160000":
        failures.append("external/QS3D-Platform must be a gitlink")
    elif fields[1].lower() != EXPECTED_PLATFORM:
        failures.append(f"Platform pin mismatch: expected {EXPECTED_PLATFORM}, got {fields[1].lower()}")

    return report(failures)


def report(failures: list[str]) -> int:
    if failures:
        print("Cubicost standalone parity guard FAILED")
        for failure in failures:
            print(" - " + failure)
        return 1
    print(f"Cubicost standalone parity guard PASS ({EXPECTED_PLATFORM})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
