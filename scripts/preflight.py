#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "src"

FORBIDDEN_BINARY_SUFFIXES = {
    ".dll", ".exe", ".lib", ".a", ".so", ".dylib", ".pdb", ".nupkg"
}
FORBIDDEN_BRICSCAD_TOKENS = [
    "BrxMgd",
    "TD_Mgd",
    "Bricscad.ApplicationServices",
    "Bricscad.DatabaseServices",
    "Teigha.DatabaseServices",
    "Teigha.Runtime",
]

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


for required in ["PLANNING.md", "README.md", "docs/NATIVE-SDK-BOUNDARY.md", ".gitmodules"]:
    if not (ROOT / required).is_file():
        fail(f"missing required repository boundary file: {required}")

modules = ROOT / ".gitmodules"
if modules.is_file():
    modules_text = modules.read_text(encoding="utf-8")
    if "external/QS3D-Platform" not in modules_text or "trinhtanphat/QS3D-Platform" not in modules_text:
        fail("QS3D-Platform must remain an explicit pinned submodule under external/QS3D-Platform")

for path in SRC.rglob("*"):
    if not path.is_file():
        continue
    if path.suffix.lower() in FORBIDDEN_BINARY_SUFFIXES:
        fail(f"committed native/vendor binary is forbidden: {path.relative_to(ROOT)}")
    if path.suffix.lower() not in {".cs", ".csproj", ".props", ".targets", ".xaml"}:
        continue
    text = path.read_text(encoding="utf-8", errors="replace")
    for token in FORBIDDEN_BRICSCAD_TOKENS:
        if token in text:
            fail(f"BricsCAD host token {token!r} leaked into standalone source: {path.relative_to(ROOT)}")

native_dir = SRC / "QS3D.Cad.Native.Oda.Bootstrap"
if not native_dir.is_dir():
    fail("native SDK bootstrap project is missing")
else:
    for path in native_dir.rglob("*"):
        if path.is_file() and path.suffix.lower() in FORBIDDEN_BINARY_SUFFIXES:
            fail(f"native SDK bootstrap must not redistribute SDK binary: {path.relative_to(ROOT)}")

store = SRC / "QS3D.Cad.Host" / "BootstrapDrawingStore.cs"
if not store.is_file():
    fail("bootstrap persistence store is missing")
else:
    store_text = store.read_text(encoding="utf-8")
    match = re.search(r"CurrentSchema\s*=\s*(\d+)", store_text)
    if not match or int(match.group(1)) < 3:
        fail("bootstrap schema must preserve at least CAD + semantic + layer state")
    if "MinimumReadableSchema = 1" not in store_text:
        fail("bootstrap persistence must retain explicit schema-1 backward-read boundary")

app = SRC / "QS3D.Cad.Host" / "StandaloneCadApplication.cs"
if not app.is_file():
    fail("standalone application host is missing")
else:
    app_text = app.read_text(encoding="utf-8")
    for required in ["BuiltInCommands.RegisterAll", "LayerCommands.RegisterAll", "SemanticCommands.RegisterAll"]:
        if required not in app_text:
            fail(f"standalone application must register {required}")

smoke = ROOT / "tests" / "QS3D.Cad.SmokeTests" / "QS3D.Cad.SmokeTests.csproj"
if not smoke.is_file():
    fail("standalone deterministic smoke project is missing")

try:
    pin = subprocess.run(
        ["git", "rev-parse", "HEAD:external/QS3D-Platform"],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    if pin.returncode == 0:
        value = pin.stdout.strip()
        if not re.fullmatch(r"[0-9a-fA-F]{40}", value):
            fail("Platform gitlink is not an exact commit SHA")
except OSError:
    pass

if errors:
    print("QS3D CAD preflight FAILED", file=sys.stderr)
    for error in errors:
        print(f"- {error}", file=sys.stderr)
    raise SystemExit(1)

print("QS3D CAD preflight PASS")
print("checked product separation, bootstrap persistence, Platform pin and vendor-binary boundaries")
